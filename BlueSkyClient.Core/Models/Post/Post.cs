using BlueSkyClient.Core.Models.User;
using System.Text.Json.Serialization;

namespace BlueSkyClient.Core.Models.Post
{
    public class Post
    {

        [JsonPropertyName("record")]
        public PostRecord Record { get; set; }

        [JsonPropertyName("likeCount")]
        public int LikeCount { get; set; }

        [JsonPropertyName("repostCount")]
        public int RepostCount { get; set; }

        [JsonPropertyName("replyCount")]
        public int ReplyCount { get; set; }
    }
}
