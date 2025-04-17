using System.Net.Http.Json;
using BlueSkyClient.Core.Models.Post;
using BlueSkyClient.Core.Models.Feed;
using BlueSkyClient.Core.Models.User;
using BlueSkyClient.Core.Models.Auth;
using BlueSkyClient.Core.Configuration;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using System.Net.WebSockets;
using Newtonsoft.Json;
using BlueSkyClient.Core.Utilities;
using System.Text;

namespace BlueSkyClient.Core
{
    public class BlueSkyService
    {
        private readonly HttpClient _httpClient;
        private readonly BlueSkyConfig _config;
        private readonly ILogger<BlueSkyService> _logger;
        private string _accessJwt;
        private string _userDid;

        public BlueSkyService(BlueSkyConfig config, ILogger<BlueSkyService> logger = null)
        {
            _config = config ?? new BlueSkyConfig();
            _logger = logger;
            _httpClient = new HttpClient { BaseAddress = new Uri(_config.BaseUrl) };
        }

        private async Task<T> HandleApiResponse<T>(HttpResponseMessage response)
        {
            try
            {
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<T>();
            }
            catch (HttpRequestException ex)
            {
                _logger?.LogError(ex, "API request failed");
                throw new Exception($"API request failed: {ex.Message}");
            }
            catch (System.Text.Json.JsonException ex)
            {
                _logger?.LogError(ex, "Failed to parse API response");
                throw new Exception($"Failed to parse API response: {ex.Message}");
            }
        }
        public async Task AuthenticateAsync(string identifier, string password)
        {
            var response = await _httpClient.PostAsJsonAsync("com.atproto.server.createSession", new
            {
                identifier,
                password
            });

            var content = await HandleApiResponse<SessionResponse>(response);
            _accessJwt = content.AccessJwt;
            _userDid = content.Did;  // Store the DID
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessJwt);

            _logger?.LogInformation("Authentication successful for user: {identifier}, DID: {did}",
                identifier, _userDid);
        }

        public async Task<string> CreatePostAsync(string text)
        {
            if (string.IsNullOrEmpty(_userDid))
            {
                throw new InvalidOperationException("User is not authenticated. Please authenticate first.");
            }

            var postRequest = new CreatePostRequest
            {
                Repo = _userDid,
                Record = new PostRecord
                {
                    Text = text,
                    CreatedAt = DateTime.UtcNow
                }
            };

            try
            {
                _logger?.LogInformation("Attempting to create post with text: {text}", text);

                var jsonRequest = System.Text.Json.JsonSerializer.Serialize(postRequest, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                _logger?.LogDebug("Request body: {request}", jsonRequest);

                var response = await _httpClient.PostAsJsonAsync("com.atproto.repo.createRecord", postRequest);

                var responseBody = await response.Content.ReadAsStringAsync();
                _logger?.LogDebug("Response status: {status}, body: {body}",
                    response.StatusCode, responseBody);

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Failed to create post. Status: {response.StatusCode}, Response: {responseBody}");
                }

                var result = await response.Content.ReadFromJsonAsync<CreatePostResponse>();
                _logger?.LogInformation("Post created successfully with URI: {uri}", result.Uri);
                return result.Uri;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error creating post");
                throw;
            }
        }

        public async Task<List<string>> GetFollowersAsync(
         string userId,
         int? pageSize = null,
         CancellationToken cancellationToken = default)
        {
            try
            {
                var followers = new List<string>();
                string cursor = null;
                var limit = pageSize ?? 100; // Increased default page size
                var pageCount = 0;
                var retryCount = 0;
                const int maxRetries = 3;

                _logger?.LogInformation($"\nFetching followers for {userId}...");

                do
                {
                    try
                    {
                        pageCount++;
                        var url = $"app.bsky.graph.getFollowers?actor={userId}&limit={limit}" +
                                 (cursor != null ? $"&cursor={cursor}" : "");

                        _logger?.LogDebug("Fetching page {pageCount}, URL: {url}", pageCount, url);

                        var response = await _httpClient.GetAsync(url, cancellationToken);

                        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                        {
                            if (retryCount < maxRetries)
                            {
                                retryCount++;
                                _logger?.LogWarning("Rate limited, waiting before retry {retry}/3", retryCount);
                                await Task.Delay(5000, cancellationToken);
                                continue;
                            }
                            throw new Exception("Rate limit exceeded after 3 retries");
                        }

                        var content = await HandleApiResponse<FollowersResponse>(response);

                        // Add new followers
                        var newFollowers = content.Followers
                            .Select(f => f.Did)
                            .Where(did => !followers.Contains(did))
                            .ToList();

                        followers.AddRange(newFollowers);

                        _logger?.LogDebug("Page {pageCount}: Retrieved {count} followers ({newCount} new)",
                            pageCount, content.Followers.Count, newFollowers.Count);

                        cursor = content.Cursor;

                        // Add a small delay between pages to avoid rate limiting
                        if (!string.IsNullOrEmpty(cursor))
                        {
                            await Task.Delay(1000, cancellationToken);
                        }

                        cancellationToken.ThrowIfCancellationRequested();
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        if (retryCount < maxRetries)
                        {
                            retryCount++;
                            _logger?.LogWarning(ex, "Error fetching page {pageCount}, retry {retry}/3", pageCount, retryCount);
                            await Task.Delay(5000, cancellationToken);
                            continue;
                        }
                        throw;
                    }
                }
                while (!string.IsNullOrEmpty(cursor));

                _logger?.LogInformation("Retrieved total of {count} followers for user {userId}",
                    followers.Count, userId);

                return followers.Distinct().ToList(); // Ensure no duplicates
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to get followers for {userId}", userId);
                throw;
            }
        }

        public async Task<List<PostInfo>> GetUserPostsAsync(
            string userId,
            int? pageSize = null,
            CancellationToken cancellationToken = default)
        {
            var posts = new List<PostInfo>();
            string cursor = null;
            var limit = pageSize ?? _config.DefaultPageSize;

            do
            {
                var url = $"app.bsky.feed.getAuthorFeed?actor={userId}&limit={limit}" +
                         (cursor != null ? $"&cursor={cursor}" : "");

                var response = await _httpClient.GetAsync(url, cancellationToken);
                var content = await HandleApiResponse<FeedResponse>(response);

                posts.AddRange(content.Feed.Select(f => new PostInfo
                {
                    Text = f.Post.Record.Text,
                    PostedAt = f.Post.Record.CreatedAt,
                    LikeCount = f.Post.LikeCount,
                    RepostCount = f.Post.RepostCount,
                    ReplyCount = f.Post.ReplyCount
                }));

                cursor = content.Cursor;
                _logger?.LogDebug("Retrieved {count} posts for user {userId}",
                    content.Feed.Count, userId);

                cancellationToken.ThrowIfCancellationRequested();
            }
            while (!string.IsNullOrEmpty(cursor));

            _logger?.LogInformation("Retrieved total of {count} posts for user {userId}",
                posts.Count, userId);
            return posts;
        }
        public async Task StartMessageStreamAsync(Action<string> messageHandler, CancellationToken cancellationToken = default)
        {
            var endpoints = new[]
            {
        "wss://jetstream2.us-west.bsky.network/subscribe"
    };

            var retryCount = 0;
            const int maxRetries = 3;

            while (retryCount < maxRetries && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    messageHandler($"\n🔵 BlueSky Network Firehose - Attempt {retryCount + 1}/{maxRetries}");
                    messageHandler("Looking for new posts...\n");

                    foreach (var endpoint in endpoints)
                    {
                        try
                        {
                            using var ws = new ClientWebSocket();
                            await ws.ConnectAsync(new Uri(endpoint), cancellationToken);
                            messageHandler("✅ Connected! Streaming posts...\n");

                            var buffer = new byte[256 * 1024];
                            var messageCount = 0;
                            var postCount = 0;
                            var messageBuffer = new List<byte>();

                            while (!cancellationToken.IsCancellationRequested && ws.State == WebSocketState.Open)
                            {
                                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                                if (result.MessageType == WebSocketMessageType.Close)
                                {
                                    messageHandler("\nConnection closed by server");
                                    break;
                                }

                                if (result.Count > 0)
                                {
                                    messageBuffer.AddRange(new ArraySegment<byte>(buffer, 0, result.Count));

                                    if (result.EndOfMessage)
                                    {
                                        messageCount++;
                                        var data = Encoding.UTF8.GetString(messageBuffer.ToArray());

                                        try
                                        {
                                            var messages = data.Split(new[] { "}{" }, StringSplitOptions.RemoveEmptyEntries)
                                                .Select(m => m.StartsWith("{") ? m : "{" + m)
                                                .Select(m => m.EndsWith("}") ? m : m + "}");

                                            foreach (var message in messages)
                                            {
                                                var commitData = JsonConvert.DeserializeObject<dynamic>(message);

                                                if (commitData.commit != null &&
                                                    (string)commitData.commit.collection == "app.bsky.feed.post")
                                                {
                                                    var record = commitData.commit.record;
                                                    if (record != null)
                                                    {
                                                        postCount++;

                                                        bool isReply = false;
                                                        string replyTo = "";

                                                        try
                                                        {
                                                            if (record.reply != null)
                                                            {
                                                                isReply = true;
                                                                if (record.reply.parent?.uri != null)
                                                                {
                                                                    replyTo = record.reply.parent.uri.ToString();
                                                                }
                                                                else if (record.reply.root?.uri != null)
                                                                {
                                                                    replyTo = record.reply.root.uri.ToString();
                                                                }
                                                            }
                                                        }
                                                        catch (Exception ex)
                                                        {
                                                            _logger?.LogWarning("Error parsing reply data: {error}", ex.Message);
                                                        }

                                                        var content = new
                                                        {
                                                            Timestamp = record.createdAt,
                                                            PostId = (string)commitData.commit.rkey,
                                                            Did = ((string)commitData.did).Replace("did:plc:", ""),
                                                            Text = (string)record.text ?? "",
                                                            IsReply = isReply,
                                                            ReplyTo = isReply ? replyTo.Split('/').Last() : ""
                                                        };

                                                        var output = $@"
+-- Post {postCount:D4} ----------------------
| Time: {content.Timestamp:yyyy-MM-dd HH:mm:ss}
| PostID: {content.PostId}
| UserID: {content.Did}
| Reply: {(content.IsReply ? "YES -> " + content.ReplyTo : "NO")}
|
| {TextUtils.WrapText(content.Text, 50)}
+----------------------------------------";
                                                        messageHandler(output);
                                                    }
                                                }
                                            }

                                            if (messageCount % 1000 == 0)
                                            {
                                                messageHandler($"\n📊 Stats: {messageCount:N0} messages, {postCount:N0} posts ({postCount * 100.0 / messageCount:F1}%)\n");
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            if (messageCount <= 5)
                                            {
                                                messageHandler($"\nError processing message {messageCount}: {ex.Message}");
                                            }
                                        }

                                        messageBuffer.Clear();
                                    }
                                }
                            }
                            return;
                        }
                        catch (WebSocketException wsEx)
                        {
                            messageHandler($"Failed to connect to {endpoint}: {wsEx.Message}");
                            continue;
                        }
                    }
                    retryCount++;
                    await Task.Delay(5000, cancellationToken);
                }
                catch (Exception ex)
                {
                    messageHandler($"\n❌ Stream error: {ex.Message}");
                    retryCount++;
                    if (retryCount < maxRetries)
                    {
                        messageHandler($"Retrying in 5 seconds... ({retryCount}/{maxRetries})");
                        await Task.Delay(5000, cancellationToken);
                    }
                }
            }
        }
        public async Task<PostMetrics> GetPostMetricsAsync(string handle, string postId)
        {
            try
            {
                _logger?.LogInformation($"Fetching metrics for post: {postId} from handle: {handle}");

                // First get the DID from handle
                var profileResponse = await _httpClient.GetAsync($"app.bsky.actor.getProfile?actor={handle}");
                if (!profileResponse.IsSuccessStatusCode)
                {
                    throw new Exception($"Failed to get profile for handle {handle}");
                }

                var profileContent = await profileResponse.Content.ReadAsStringAsync();
                var profileData = JsonDocument.Parse(profileContent).RootElement;
                var did = profileData.GetProperty("did").GetString();

                // Get the post thread which contains engagement metrics
                var response = await _httpClient.GetAsync($"app.bsky.feed.getPostThread?uri=at://{did}/app.bsky.feed.post/{postId}");

                if (!response.IsSuccessStatusCode)
                {
                    _logger?.LogError($"API returned {response.StatusCode}");
                    throw new Exception($"Failed to get post metrics. Status: {response.StatusCode}");
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var threadData = JsonDocument.Parse(responseContent).RootElement;

                // Extract metrics from the post data
                var post = threadData.GetProperty("thread").GetProperty("post");

                var metrics = new PostMetrics
                {
                    LikeCount = post.GetProperty("likeCount").GetInt32(),
                    RepostCount = post.GetProperty("repostCount").GetInt32(),
                    ReplyCount = post.GetProperty("replyCount").GetInt32(),
                    TotalEngagement = 0 
                };

                // Calculate total engagement
                metrics.TotalEngagement = metrics.LikeCount + metrics.RepostCount + metrics.ReplyCount;

                _logger?.LogInformation($"Successfully retrieved metrics for post {postId}");
                return metrics;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to get metrics for post {postId}");
                throw new Exception($"Failed to get post metrics: {ex.Message}", ex);
            }
        }
        public async Task<List<string>> GetPostMediaUrlsAsync(string handle, string postId)
        {
            try
            {
                _logger?.LogInformation($"Fetching media for post: {postId} from handle: {handle}");

                // First get the DID from handle
                var profileResponse = await _httpClient.GetAsync($"app.bsky.actor.getProfile?actor={handle}");
                if (!profileResponse.IsSuccessStatusCode)
                {
                    _logger?.LogError($"Failed to get profile for handle {handle}");
                    return new List<string>();
                }

                var profileContent = await profileResponse.Content.ReadAsStringAsync();
                var profileData = JsonConvert.DeserializeObject<dynamic>(profileContent);
                var did = (string)profileData.did;

                // Now get the post thread
                var response = await _httpClient.GetAsync($"app.bsky.feed.getPostThread?uri=at://{did}/app.bsky.feed.post/{postId}");

                if (!response.IsSuccessStatusCode)
                {
                    _logger?.LogError($"API returned {response.StatusCode}");
                    _logger?.LogError(await response.Content.ReadAsStringAsync());
                    return new List<string>();
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                _logger?.LogDebug($"Response: {responseContent}");

                var threadData = JsonConvert.DeserializeObject<dynamic>(responseContent);
                var mediaUrls = new List<string>();

                if (threadData?.thread?.post?.embed != null)
                {
                    var embed = threadData.thread.post.embed;
                    var embedType = (string)embed["$type"];

                    _logger?.LogDebug($"Found embed type: {embedType}");

                    if (embedType == "app.bsky.embed.images#view")
                    {
                        foreach (var image in embed.images)
                        {
                            mediaUrls.Add((string)image.fullsize);
                            _logger?.LogDebug($"Found image URL: {image.fullsize}");
                        }
                    }
                    else if (embedType == "app.bsky.embed.recordWithMedia#view" &&
                             embed.media != null &&
                             (string)embed.media["$type"] == "app.bsky.embed.images#view")
                    {
                        foreach (var image in embed.media.images)
                        {
                            mediaUrls.Add((string)image.fullsize);
                            _logger?.LogDebug($"Found image URL: {image.fullsize}");
                        }
                    }
                }

                return mediaUrls;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to get post media");
                throw;
            }
        }
        public async Task<string> GetHandleFromDidAsync(string did)
        {
            try
            {
                _logger?.LogInformation($"Fetching handle for DID: {did}");

                // If DID doesn't start with "did:plc:", add it
                if (!did.StartsWith("did:plc:"))
                {
                    did = $"did:plc:{did}";
                }

                var response = await _httpClient.GetAsync($"app.bsky.actor.getProfile?actor={did}");

                if (!response.IsSuccessStatusCode)
                {
                    _logger?.LogError($"API returned {response.StatusCode}");
                    _logger?.LogError(await response.Content.ReadAsStringAsync());
                    throw new Exception($"Failed to get profile for DID: {did}");
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                _logger?.LogDebug($"Response: {responseContent}");

                var profileData = JsonConvert.DeserializeObject<dynamic>(responseContent);
                var handle = (string)profileData.handle;

                _logger?.LogInformation($"Found handle {handle} for DID {did}");
                return handle;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to get handle for DID {did}");
                throw new Exception($"Failed to get handle: {ex.Message}");
            }
        }
        public async Task<ProfileInfo> GetProfileInfoAsync(string handle)
        {
            try
            {
                _logger?.LogInformation($"Fetching profile info for: {handle}");

                // Get basic profile info
                var response = await _httpClient.GetAsync($"app.bsky.actor.getProfile?actor={handle}");

                if (!response.IsSuccessStatusCode)
                {
                    _logger?.LogError($"API returned {response.StatusCode}");
                    _logger?.LogError(await response.Content.ReadAsStringAsync());
                    throw new Exception($"Failed to get profile for handle: {handle}");
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                _logger?.LogDebug($"Profile Response: {responseContent}");

                var profileData = JsonConvert.DeserializeObject<dynamic>(responseContent);

                // Create profile info object
                var profileInfo = new ProfileInfo
                {
                    Handle = (string)profileData.handle,
                    Did = (string)profileData.did,
                    DisplayName = (string)profileData.displayName ?? handle,
                    Description = (string)profileData.description ?? "",
                    Avatar = (string)profileData.avatar ?? "",
                    Banner = (string)profileData.banner ?? "",
                    FollowersCount = (int)profileData.followersCount,
                    FollowingCount = (int)profileData.followsCount,
                    PostsCount = (int)profileData.postsCount,
                    Labels = new ProfileLabels
                    {
                        IsAdmin = HasLabel(profileData.labels, "admin"),
                        IsModerator = HasLabel(profileData.labels, "moderator"),
                        IsVerified = HasLabel(profileData.labels, "verified")
                    }
                };

                _logger?.LogInformation($"Successfully retrieved profile for {handle}");
                return profileInfo;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to get profile info for {handle}");
                throw new Exception($"Failed to get profile info: {ex.Message}");
            }
        }
        public async Task<List<InteractionPost>> GetUserLikedPostsAsync(
     string userDid,
     int? pageSize = null,
     CancellationToken cancellationToken = default)
        {
            try
            {
                var likedPosts = new List<InteractionPost>();
                string cursor = null;
                var limit = pageSize ?? _config.DefaultPageSize;
                var hasMorePages = true;

                // Ensure DID has the correct prefix
                if (!userDid.StartsWith("did:plc:"))
                {
                    userDid = $"did:plc:{userDid}";
                }

                while (hasMorePages)
                {
                    var url = $"app.bsky.feed.getActorLikes?actor={userDid}&limit={limit}" +
                             (cursor != null ? $"&cursor={cursor}" : "");

                    var response = await _httpClient.GetAsync(url, cancellationToken);

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger?.LogError($"API returned {response.StatusCode}");
                        throw new Exception($"Failed to get likes for user: {userDid}");
                    }

                    var responseContent = await response.Content.ReadAsStringAsync();
                    var feedData = JsonDocument.Parse(responseContent).RootElement;
                    var feed = feedData.GetProperty("feed");

                    // Check if we got any posts in this page
                    if (feed.GetArrayLength() == 0)
                    {
                        hasMorePages = false;
                        break;
                    }

                    foreach (var item in feed.EnumerateArray())
                    {
                        var post = item.GetProperty("post");
                        var record = post.GetProperty("record");

                        var interactionPost = new InteractionPost
                        {
                            Did = post.GetProperty("author").GetProperty("did").GetString(),
                            CreatedAt = DateTime.Parse(record.GetProperty("createdAt").GetString()),
                            Text = record.GetProperty("text").GetString(),
                            PostId = post.GetProperty("uri").GetString().Split('/').Last()
                        };

                        // Check for parent post if it exists
                        if (record.TryGetProperty("reply", out var reply))
                        {
                            if (reply.TryGetProperty("parent", out var parent))
                            {
                                interactionPost.ParentPostId = parent.GetProperty("uri").GetString().Split('/').Last();
                            }
                        }

                        likedPosts.Add(interactionPost);
                    }

                    // Update cursor and check if we should continue
                    if (feedData.TryGetProperty("cursor", out var cursorElement))
                    {
                        cursor = cursorElement.GetString();
                        // If we got a cursor but no items, or cursor is empty, stop
                        if (string.IsNullOrEmpty(cursor) || feed.GetArrayLength() == 0)
                        {
                            hasMorePages = false;
                        }
                    }
                    else
                    {
                        hasMorePages = false;
                    }

                    cancellationToken.ThrowIfCancellationRequested();

                    // Add delay to avoid rate limiting
                    if (hasMorePages)
                    {
                        await Task.Delay(1000, cancellationToken);
                    }
                }

                _logger?.LogInformation($"Retrieved {likedPosts.Count} liked posts for user {userDid}");
                return likedPosts;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to get liked posts for user {userDid}");
                throw new Exception($"Failed to get liked posts: {ex.Message}");
            }
        }

        public async Task<List<ReplyPost>> GetUserRepliesAsync(
             string userDid,
             int? pageSize = null,
             CancellationToken cancellationToken = default)
        {
            try
            {
                var replies = new List<ReplyPost>();
                string cursor = null;
                var hasMorePages = true;
                var limit = pageSize ?? _config.DefaultPageSize;

                // Ensure DID has the correct prefix
                if (!userDid.StartsWith("did:plc:"))
                {
                    userDid = $"did:plc:{userDid}";
                }

                while (hasMorePages)
                {
                    var url = $"app.bsky.feed.getAuthorFeed?actor={userDid}&limit={limit}" +
                             (cursor != null ? $"&cursor={cursor}" : "");

                    var response = await _httpClient.GetAsync(url, cancellationToken);
                    if (!response.IsSuccessStatusCode)
                    {
                        _logger?.LogError($"API returned {response.StatusCode}");
                        throw new Exception($"Failed to get replies for user: {userDid}");
                    }

                    var responseContent = await response.Content.ReadAsStringAsync();
                    var feedData = JsonDocument.Parse(responseContent).RootElement;
                    var feed = feedData.GetProperty("feed");

                    // Check if we got any posts in this page
                    if (feed.GetArrayLength() == 0)
                    {
                        hasMorePages = false;
                        break;
                    }

                    foreach (var item in feed.EnumerateArray())
                    {
                        var post = item.GetProperty("post");
                        var record = post.GetProperty("record");

                        // Only process replies
                        if (record.TryGetProperty("reply", out var reply))
                        {
                            var replyPost = new ReplyPost
                            {
                                Did = post.GetProperty("author").GetProperty("did").GetString(),
                                CreatedAt = DateTime.Parse(record.GetProperty("createdAt").GetString()),
                                Text = record.GetProperty("text").GetString(),
                                PostId = post.GetProperty("uri").GetString().Split('/').Last(),
                                ParentPostId = reply.GetProperty("parent").GetProperty("uri").GetString().Split('/').Last()
                            };

                            // For each reply, fetch its thread to get nested replies
                            try
                            {
                                var threadUrl = $"app.bsky.feed.getPostThread?uri=at://{replyPost.Did}/app.bsky.feed.post/{replyPost.PostId}";
                                var threadResponse = await _httpClient.GetAsync(threadUrl, cancellationToken);

                                if (threadResponse.IsSuccessStatusCode)
                                {
                                    var threadContent = await threadResponse.Content.ReadAsStringAsync();
                                    var threadData = JsonDocument.Parse(threadContent).RootElement;

                                    if (threadData.TryGetProperty("thread", out var thread))
                                    {
                                        if (thread.TryGetProperty("replies", out var threadReplies))
                                        {
                                            foreach (var nestedReply in threadReplies.EnumerateArray())
                                            {
                                                var nestedPost = nestedReply.GetProperty("post");
                                                var nestedRecord = nestedPost.GetProperty("record");

                                                var nestedReplyPost = new ReplyPost
                                                {
                                                    Did = nestedPost.GetProperty("author").GetProperty("did").GetString(),
                                                    CreatedAt = DateTime.Parse(nestedRecord.GetProperty("createdAt").GetString()),
                                                    Text = nestedRecord.GetProperty("text").GetString(),
                                                    PostId = nestedPost.GetProperty("uri").GetString().Split('/').Last(),
                                                    ParentPostId = replyPost.PostId
                                                };

                                                replyPost.Replies.Add(nestedReplyPost);
                                            }
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogWarning($"Failed to fetch nested replies for post {replyPost.PostId}: {ex.Message}");
                            }

                            replies.Add(replyPost);
                        }
                    }

                    // Update cursor and check if we should continue
                    if (feedData.TryGetProperty("cursor", out var cursorElement))
                    {
                        cursor = cursorElement.GetString();
                        if (string.IsNullOrEmpty(cursor) || feed.GetArrayLength() == 0)
                        {
                            hasMorePages = false;
                        }
                    }
                    else
                    {
                        hasMorePages = false;
                    }

                    cancellationToken.ThrowIfCancellationRequested();

                    // Add delay to avoid rate limiting
                    if (hasMorePages)
                    {
                        await Task.Delay(1000, cancellationToken);
                    }
                }

                _logger?.LogInformation($"Retrieved {replies.Count} replies for user {userDid}");
                return replies;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to get replies for user {userDid}");
                throw new Exception($"Failed to get replies: {ex.Message}");
            }
        }

        private bool HasLabel(dynamic labels, string labelType)
        {
            if (labels == null) return false;

            foreach (var label in labels)
            {
                if ((string)label.val == labelType)
                    return true;
            }
            return false;
        }
    }

}