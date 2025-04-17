using System.Text.Json.Serialization;

namespace BlueSkyClient.Core.Models.User
{
    public class FollowersResponse
    {
        [JsonPropertyName("followers")]
        public List<Follower> Followers { get; set; }
        [JsonPropertyName("cursor")]
        public string Cursor { get; set; }
    }
}
