using BlueSkyClient.Core.Models.Post;
using System.Text;

namespace BlueSkyClient.Core.Utilities
{
    public static class TextUtils
    {
        public static string WrapText(string text, int maxLineLength)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var words = text.Split(' ');
            var lines = new List<string>();
            var currentLine = new StringBuilder();

            foreach (var word in words)
            {
                if (currentLine.Length + word.Length + 1 > maxLineLength)
                {
                    if (currentLine.Length > 0)
                        lines.Add(currentLine.ToString().Trim());
                    currentLine.Clear();
                    currentLine.Append(word);
                }
                else
                {
                    if (currentLine.Length > 0)
                        currentLine.Append(' ');
                    currentLine.Append(word);
                }
            }

            if (currentLine.Length > 0)
                lines.Add(currentLine.ToString().Trim());

            return string.Join("\n│ ", lines);
        }
        public static string GenerateRepliesXml(List<ReplyPost> replies)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendLine("<replies>");

            foreach (var reply in replies)
            {
                AppendReplyToXml(sb, reply, 1);
            }

            sb.AppendLine("</replies>");
            return sb.ToString();
        }

        public static void AppendReplyToXml(StringBuilder sb, ReplyPost reply, int indent)
        {
            var indentStr = new string(' ', indent * 2);
            sb.AppendLine($"{indentStr}<reply>");
            sb.AppendLine($"{indentStr}  <author>{System.Security.SecurityElement.Escape(reply.Did)}</author>");
            sb.AppendLine($"{indentStr}  <datetime>{reply.CreatedAt:yyyy-MM-dd HH:mm:ss}</datetime>");
            sb.AppendLine($"{indentStr}  <postId>{reply.PostId}</postId>");
            sb.AppendLine($"{indentStr}  <parentPostId>{reply.ParentPostId}</parentPostId>");
            sb.AppendLine($"{indentStr}  <text>{System.Security.SecurityElement.Escape(reply.Text)}</text>");

            if (reply.Replies.Any())
            {
                sb.AppendLine($"{indentStr}  <nestedReplies>");
                foreach (var nestedReply in reply.Replies)
                {
                    AppendReplyToXml(sb, nestedReply, indent + 2);
                }
                sb.AppendLine($"{indentStr}  </nestedReplies>");
            }

            sb.AppendLine($"{indentStr}</reply>");
        }
    }
}