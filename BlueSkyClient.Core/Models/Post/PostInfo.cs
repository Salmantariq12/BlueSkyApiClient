
using System.Text.Json.Serialization;

namespace BlueSkyClient.Core.Models.Post
{
    public class PostInfo
    {
        public string Text { get; set; }
        public DateTime PostedAt { get; set; }
        public int LikeCount { get; set; }
        public int RepostCount { get; set; }
        public int ReplyCount { get; set; }
    }

}
