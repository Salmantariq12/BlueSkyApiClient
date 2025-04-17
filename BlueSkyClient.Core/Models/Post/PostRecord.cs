using System.Text.Json.Serialization;

namespace BlueSkyClient.Core.Models.Post
{
    public class PostRecord
    {
        [JsonPropertyName("text")]
        public string Text { get; set; }

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("$type")]
        public string Type { get; set; } = "app.bsky.feed.post";

        [JsonPropertyName("langs")]
        public string[] Langs { get; set; } = new[] { "en" };

        [JsonPropertyName("facets")]
        public object[] Facets { get; set; } = Array.Empty<object>();

        [JsonPropertyName("tags")]
        public string[] Tags { get; set; } = Array.Empty<string>();
    }
}
