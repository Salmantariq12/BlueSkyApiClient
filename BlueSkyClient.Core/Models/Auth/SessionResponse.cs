using System.Text.Json.Serialization;

namespace BlueSkyClient.Core.Models.Auth
{
    public class SessionResponse
    {
        [JsonPropertyName("accessJwt")]
        public string AccessJwt { get; set; }

        [JsonPropertyName("did")]
        public string Did { get; set; }

    }
}
