using System.Text.Json.Serialization;

namespace BlueSkyClient.Core.Models.Post
{
    public class CreatePostRequest
    {
        public string Collection { get; set; } = "app.bsky.feed.post";
        [JsonPropertyName("repo")]
        public string Repo { get; set; } = "self";

        [JsonPropertyName("record")]
        public PostRecord Record { get; set; }
    }
}
