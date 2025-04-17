namespace BlueSkyClient.Core.Models.Post
{
    public class InteractionPost
    {
        public string Did { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Text { get; set; }
        public string PostId { get; set; }
        public string ParentPostId { get; set; }
    }
    public class ReplyPost : InteractionPost
    {
        public List<ReplyPost> Replies { get; set; } = new List<ReplyPost>();
    }
}
