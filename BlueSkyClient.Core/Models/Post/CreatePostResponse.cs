using System.Text.Json.Serialization;

namespace BlueSkyClient.Core.Models.Post
{
    public class CreatePostResponse
    {
        [JsonPropertyName("uri")]
        public string Uri { get; set; }
    }
}
