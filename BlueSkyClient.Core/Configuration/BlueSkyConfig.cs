namespace BlueSkyClient.Core.Configuration
{
    public class BlueSkyConfig
    {
        public string BaseUrl { get; set; } = "https://bsky.social/xrpc/";
        public int DefaultPageSize { get; set; } = 1;
        public int MaxRetries { get; set; } = 3;
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);
    }
}