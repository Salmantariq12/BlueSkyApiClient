using System.Text.Json.Serialization;

namespace BlueSkyClient.Core.Models.Feed
{
    public class FeedResponse
    {
        [JsonPropertyName("feed")]
        public List<FeedItem> Feed { get; set; }
        [JsonPropertyName("cursor")]
        public string Cursor { get; set; }
    }
}
