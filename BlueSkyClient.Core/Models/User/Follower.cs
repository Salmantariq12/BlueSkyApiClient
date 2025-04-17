using System.Text.Json.Serialization;

namespace BlueSkyClient.Core.Models.User
{
    public class Follower
    {
        [JsonPropertyName("did")]
        public string Did { get; set; }
    }
}
