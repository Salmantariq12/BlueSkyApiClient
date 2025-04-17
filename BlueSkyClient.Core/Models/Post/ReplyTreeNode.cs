namespace BlueSkyClient.Core.Models.Post
{
    public class ReplyTreeNode
    {
        public InteractionPost Post { get; set; }
        public List<ReplyTreeNode> Replies { get; set; } = new List<ReplyTreeNode>();
    }

}
