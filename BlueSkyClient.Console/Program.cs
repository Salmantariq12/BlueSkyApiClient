using BlueSkyClient.Core;
using BlueSkyClient.Core.Configuration;
using BlueSkyClient.Core.Utilities;
using Microsoft.Extensions.Logging;
using System.Text;

namespace BlueSkyClient.Console
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Setup logging
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
            });
            var logger = loggerFactory.CreateLogger<BlueSkyService>();

            // Setup configuration
            var config = new BlueSkyConfig
            {
                BaseUrl = "https://bsky.social/xrpc/",
                DefaultPageSize = 50,
                MaxRetries = 3,
                RetryDelay = TimeSpan.FromSeconds(1)
            };

            var service = new BlueSkyService(config, logger);

            // First authenticate
            System.Console.Write("Enter your BlueSky identifier (email): ");
            var identifier = System.Console.ReadLine();
            System.Console.Write("Enter your password: ");
            var password = ReadPasswordSecurely();

            try
            {
                await service.AuthenticateAsync(identifier, password);
                System.Console.WriteLine("Authentication successful!");

                while (true)
                {
                    System.Console.WriteLine("\nChoose an option:");
                    System.Console.WriteLine("1. Start message stream");
                    System.Console.WriteLine("2. Create new post");
                    System.Console.WriteLine("3. Get user followers");
                    System.Console.WriteLine("4. Get user posts");
                    System.Console.WriteLine("5. Get post media");
                    System.Console.WriteLine("6. Get handle from DID");
                    System.Console.WriteLine("7. Get profile information");
                    System.Console.WriteLine("8. Get post metrics");
                    System.Console.WriteLine("9. Get user's liked posts");
                    System.Console.WriteLine("10. Get user's replies");
                    System.Console.WriteLine("11. Exit");

                    var choice = System.Console.ReadLine();
                    using var cts = new CancellationTokenSource();

                    // Setup Ctrl+C handler
                    System.Console.CancelKeyPress += (s, e) =>
                    {
                        e.Cancel = true;
                        cts.Cancel();
                    };

                    try
                    {
                        // Force UTF-8 output encoding
                        System.Console.OutputEncoding = Encoding.UTF8;

                        switch (choice)
                        {
                            case "1":
                                System.Console.WriteLine("Starting message stream (Ctrl+C to stop)...");
                                try
                                {
                                    var streamTask = service.StartMessageStreamAsync(msg =>
                                    {
                                        System.Console.WriteLine("-------------------");
                                        System.Console.WriteLine(msg);
                                    }, cts.Token);

                                    // Wait for Ctrl+C or task completion
                                    await streamTask;
                                }
                                catch (OperationCanceledException)
                                {
                                    System.Console.WriteLine("\nStream stopped by user.");
                                }
                                catch (Exception ex)
                                {
                                    System.Console.WriteLine($"\nStream error: {ex.Message}");
                                    System.Console.WriteLine("Try again in a few moments...");
                                }
                                break;

                            case "2":
                                try
                                {
                                    System.Console.Write("Enter your post text: ");
                                    var text = System.Console.ReadLine();

                                    if (string.IsNullOrWhiteSpace(text))
                                    {
                                        System.Console.WriteLine("Post text cannot be empty.");
                                        break;
                                    }

                                    System.Console.WriteLine("Creating post...");
                                    var uri = await service.CreatePostAsync(text);
                                    System.Console.WriteLine($"Post created successfully!");
                                    System.Console.WriteLine($"URI: {uri}");

                                    // Extract the post ID from the URI for the user
                                    var postId = uri.Split('/').Last();
                                    System.Console.WriteLine($"Post ID: {postId}");
                                    System.Console.WriteLine($"View your post at: https://bsky.app/profile/{postId}");
                                }
                                catch (Exception ex)
                                {
                                    System.Console.WriteLine($"Error creating post: {ex.Message}");
                                    if (ex.InnerException != null)
                                    {
                                        System.Console.WriteLine($"Additional details: {ex.InnerException.Message}");
                                    }
                                }
                                break;

                            case "3":
                                try
                                {
                                    System.Console.Write("Enter user ID (e.g., username.bsky.social): ");
                                    var userId = System.Console.ReadLine();

                                    if (string.IsNullOrWhiteSpace(userId))
                                    {
                                        System.Console.WriteLine("User ID cannot be empty");
                                        break;
                                    }

                                    System.Console.WriteLine("\nFetching followers...");
                                    System.Console.WriteLine("Press Ctrl+C to cancel at any time.\n");

                                    var followers = await service.GetFollowersAsync(userId, null, cts.Token);

                                    System.Console.WriteLine($"\nFollowers for {userId}:");
                                    System.Console.WriteLine($"Total count: {followers.Count}");

                                    if (followers.Any())
                                    {
                                        System.Console.WriteLine("\nDo you want to see the full list? (y/n)");
                                        if (System.Console.ReadLine()?.ToLower() == "y")
                                        {
                                            foreach (var follower in followers)
                                            {
                                                System.Console.WriteLine(follower);
                                            }
                                        }
                                    }
                                }
                                catch (OperationCanceledException)
                                {
                                    System.Console.WriteLine("\nOperation cancelled by user.");
                                }
                                catch (Exception ex)
                                {
                                    System.Console.WriteLine($"Error getting followers: {ex.Message}");
                                }
                                break;

                            case "4":
                                try
                                {
                                    System.Console.Write("Enter user ID (e.g., username.bsky.social): ");
                                    var userId = System.Console.ReadLine();

                                    if (string.IsNullOrWhiteSpace(userId))
                                    {
                                        System.Console.WriteLine("User ID cannot be empty");
                                        break;
                                    }

                                    System.Console.Write("Enter page size (press Enter for default): ");
                                    var pageSizeInput = System.Console.ReadLine();
                                    int? pageSize = string.IsNullOrEmpty(pageSizeInput)
                                        ? null
                                        : int.Parse(pageSizeInput);

                                    System.Console.WriteLine("\nFetching posts...");
                                    System.Console.WriteLine("Press Ctrl+C to cancel at any time.\n");

                                    var posts = await service.GetUserPostsAsync(userId, pageSize, cts.Token);

                                    System.Console.WriteLine($"\nPosts for {userId}:");
                                    System.Console.WriteLine($"Total count: {posts.Count}\n");

                                    foreach (var post in posts)
                                    {
                                        System.Console.WriteLine($"════════════════════════════════");
                                        System.Console.WriteLine($"Posted: {post.PostedAt:yyyy-MM-dd HH:mm:ss}");
                                        System.Console.WriteLine($"Text: {post.Text}");
                                        System.Console.WriteLine($"Engagement:");
                                        System.Console.WriteLine($"  ❤️ {post.LikeCount:N0} Likes");
                                        System.Console.WriteLine($"  🔄 {post.RepostCount:N0} Reposts");
                                        System.Console.WriteLine($"  💬 {post.ReplyCount:N0} Comments");
                                        System.Console.WriteLine($"════════════════════════════════\n");
                                    }
                                }
                                catch (OperationCanceledException)
                                {
                                    System.Console.WriteLine("\nOperation cancelled by user.");
                                }
                                catch (FormatException)
                                {
                                    System.Console.WriteLine("\nInvalid page size number format.");
                                }
                                catch (Exception ex)
                                {
                                    System.Console.WriteLine($"\nError getting posts: {ex.Message}");
                                }
                                break;

                            case "5":
                                try
                                {
                                    System.Console.Write("Enter handle (e.g., username.bsky.social): ");
                                    var handle = System.Console.ReadLine();

                                    System.Console.Write("Enter post ID: ");
                                    var postId = System.Console.ReadLine();

                                    if (string.IsNullOrWhiteSpace(handle) || string.IsNullOrWhiteSpace(postId))
                                    {
                                        System.Console.WriteLine("Handle and Post ID cannot be empty");
                                        break;
                                    }

                                    System.Console.WriteLine("\nFetching media...");
                                    var mediaUrls = await service.GetPostMediaUrlsAsync(handle, postId);

                                    if (!mediaUrls.Any())
                                    {
                                        System.Console.WriteLine("No media found in this post.");
                                        break;
                                    }

                                    System.Console.WriteLine($"\nFound {mediaUrls.Count} images:");
                                    foreach (var url in mediaUrls)
                                    {
                                        System.Console.WriteLine($"- {url}");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    System.Console.WriteLine($"\nError getting media: {ex.Message}");
                                }
                                break;
                            case "6":
                                try
                                {
                                    System.Console.Write("Enter DID (e.g., 3pqb6zt3wmsgjfxd36en45pg): ");
                                    var did = System.Console.ReadLine();

                                    if (string.IsNullOrWhiteSpace(did))
                                    {
                                        System.Console.WriteLine("DID cannot be empty");
                                        break;
                                    }

                                    System.Console.WriteLine("\nFetching handle...");
                                    var handle = await service.GetHandleFromDidAsync(did);
                                    System.Console.WriteLine($"\nHandle for DID {did}: {handle}");
                                }
                                catch (Exception ex)
                                {
                                    System.Console.WriteLine($"\nError getting handle: {ex.Message}");
                                }
                                break;

                            case "7": 
                                try
                                {
                                    System.Console.Write("Enter handle (e.g., username.bsky.social): ");
                                    var handle = System.Console.ReadLine();

                                    if (string.IsNullOrWhiteSpace(handle))
                                    {
                                        System.Console.WriteLine("Handle cannot be empty");
                                        break;
                                    }

                                    System.Console.WriteLine("\nFetching profile information...");
                                    var profile = await service.GetProfileInfoAsync(handle);

                                    var output = $@"
╭─ Profile Information ─────────────────────
│ Handle: {profile.Handle}
│ Name: {profile.DisplayName}
│ DID: {profile.Did}
│
│ Stats:
│   • Followers: {profile.FollowersCount:N0}
│   • Following: {profile.FollowingCount:N0}
│   • Posts: {profile.PostsCount:N0}
│
│ Images:
│   • Avatar: {(string.IsNullOrEmpty(profile.Avatar) ? "None" : profile.Avatar)}
│   • Banner: 
                                    {(string.IsNullOrEmpty(profile.Banner) ? "None" : profile.Banner)}
│
│ Bio:
│ {TextUtils.WrapText(profile.Description ?? "No description", 50)}
╰────────────────────────────────────────";

                                    System.Console.WriteLine(output);
                                }
                                catch (Exception ex)
                                {
                                    System.Console.WriteLine($"\nError getting profile: {ex.Message}");
                                }
                                break;
                            case "8":
                                try
                                {
                                    System.Console.Write("Enter handle (e.g., username.bsky.social): ");
                                    var handle = System.Console.ReadLine();

                                    System.Console.Write("Enter post ID: ");
                                    var postId = System.Console.ReadLine();

                                    if (string.IsNullOrWhiteSpace(handle) || string.IsNullOrWhiteSpace(postId))
                                    {
                                        System.Console.WriteLine("Handle and Post ID cannot be empty");
                                        break;
                                    }

                                    System.Console.WriteLine("\nFetching post metrics...");
                                    var metrics = await service.GetPostMetricsAsync(handle, postId);

                                    var output = $@"
╭─ Post Metrics ─────────────────────
│ 
│ ❤️  Likes: {metrics.LikeCount:N0}
│ 🔄 Reposts: {metrics.RepostCount:N0}
│ 💬 Comments: {metrics.ReplyCount:N0}
│ 
│ 📊 Total Engagement: {metrics.TotalEngagement:N0}
╰────────────────────────────────────";

                                    System.Console.WriteLine(output);
                                }
                                catch (Exception ex)
                                {
                                    System.Console.WriteLine($"\nError getting post metrics: {ex.Message}");
                                }
                                break;

                            case "9":
                                try
                                {
                                    System.Console.Write("Enter DID (e.g., did:plc:userId or just userId): ");
                                    var userDid = System.Console.ReadLine();

                                    if (string.IsNullOrWhiteSpace(userDid))
                                    {
                                        System.Console.WriteLine("DID cannot be empty");
                                        break;
                                    }

                                    System.Console.Write("Enter page size (press Enter for default): ");
                                    var pageSizeInput = System.Console.ReadLine();
                                    int? pageSize = string.IsNullOrEmpty(pageSizeInput)
                                        ? null
                                        : int.Parse(pageSizeInput);

                                    System.Console.WriteLine("\nFetching liked posts...");
                                    System.Console.WriteLine("Press Ctrl+C to cancel at any time.\n");

                                    var likedPosts = await service.GetUserLikedPostsAsync(userDid, pageSize, cts.Token);

                                    System.Console.WriteLine($"\nLiked Posts for {userDid}:");
                                    System.Console.WriteLine($"Total count: {likedPosts.Count}\n");

                                    foreach (var post in likedPosts)
                                    {
                                        System.Console.WriteLine("════════════════════════════════");
                                        System.Console.WriteLine($"Posted: {post.CreatedAt:yyyy-MM-dd HH:mm:ss}");
                                        System.Console.WriteLine($"Author: {post.Did}");
                                        System.Console.WriteLine($"Post ID: {post.PostId}");
                                        if (!string.IsNullOrEmpty(post.ParentPostId))
                                        {
                                            System.Console.WriteLine($"In Reply To: {post.ParentPostId}");
                                        }
                                        System.Console.WriteLine($"\nText: {post.Text}");
                                        System.Console.WriteLine("════════════════════════════════\n");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    System.Console.WriteLine($"\nError getting liked posts: {ex.Message}");
                                }
                                break;

                            case "10":
                                try
                                {
                                    System.Console.Write("Enter DID (e.g., did:plc:userId or just userId): ");
                                    var userDid = System.Console.ReadLine();

                                    if (string.IsNullOrWhiteSpace(userDid))
                                    {
                                        System.Console.WriteLine("DID cannot be empty");
                                        break;
                                    }

                                    System.Console.Write("Enter page size (press Enter for default): ");
                                    var pageSizeInput = System.Console.ReadLine();
                                    int? pageSize = string.IsNullOrEmpty(pageSizeInput)
                                        ? null
                                        : int.Parse(pageSizeInput);

                                    System.Console.WriteLine("\nFetching replies...");

                                    var replies = await service.GetUserRepliesAsync(userDid, pageSize, cts.Token);
                                    var xml = TextUtils.GenerateRepliesXml(replies);

                                    System.Console.WriteLine("\nReply Tree XML:");
                                    System.Console.WriteLine(xml);
                                }
                                catch (Exception ex)
                                {
                                    System.Console.WriteLine($"\nError getting replies: {ex.Message}");
                                }
                                break;

                            case "11":
                                System.Environment.Exit(0);
                                break;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        System.Console.WriteLine("\nOperation cancelled by user");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error: {ex.Message}");
                logger.LogError(ex, "Application error");
            }
        }

        private static string ReadPasswordSecurely()
        {
            var password = new StringBuilder();
            while (true)
            {
                var key = System.Console.ReadKey(true);
                if (key.Key == ConsoleKey.Enter)
                    break;
                if (key.Key == ConsoleKey.Backspace && password.Length > 0)
                {
                    password.Length--;
                    System.Console.Write("\b \b");
                }
                else if (key.Key != ConsoleKey.Backspace)
                {
                    password.Append(key.KeyChar);
                    System.Console.Write("*");
                }
            }
            System.Console.WriteLine();
            return password.ToString();
        }
    }
}