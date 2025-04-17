namespace BlueSkyClient.Core.Models.User
{
    public class ProfileInfo
    {
        public string Handle { get; set; }
        public string Did { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string Avatar { get; set; }
        public string Banner { get; set; }
        public int FollowersCount { get; set; }
        public int FollowingCount { get; set; }
        public int PostsCount { get; set; }
        public ProfileLabels Labels { get; set; }
    }

 
}
