using System.Text.Json.Serialization;

namespace BlueSkyClient.Core.Models.User
{
    public class Author
    {
        [JsonPropertyName("handle")]
        public string Handle { get; set; }
    }
}
