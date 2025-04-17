using System.Text.Json.Serialization;
namespace BlueSkyClient.Core.Models.Feed
{
    public class FeedItem
    {
        [JsonPropertyName("post")]
        public Post.Post Post { get; set; }
    }
}
