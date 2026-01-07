# BlueSkyApiClient

  A C# client library for interacting with the BlueSky (AT Protocol) social network API.

  ## Features

  - 🔐 Authentication & session management
  - 📝 Create and manage posts
  - 👥 Follow/unfollow users
  - 🔍 Search functionality
  - 📊 Feed retrieval
  - ⚡ Async/await support

  ## Tech Stack

  - C#
  - .NET
  - AT Protocol
  - HTTP Client

  ## Prerequisites

  - .NET 6.0 or later
  - BlueSky account

  ## Getting Started

  1. Clone the repository
  ```bash
  git clone https://github.com/Salmantariq12/BlueSkyApiClient.git

  2. Install the package or reference the project
  3. Initialize the client
  var client = new BlueSkyClient();
  await client.LoginAsync("your-handle.bsky.social", "your-app-password");

  Usage Examples

  // Login
  var client = new BlueSkyClient();
  await client.LoginAsync("handle.bsky.social", "app-password");

  // Create a post
  await client.CreatePostAsync("Hello from C#! 🚀");

  // Get your feed
  var feed = await client.GetTimelineAsync(limit: 50);

  // Follow a user
  await client.FollowAsync("did:plc:xyz123");

  API Coverage

  - ✅ Authentication
  - ✅ Posts (create, delete, like, repost)
  - ✅ Profile management
  - ✅ Follow/unfollow
  - ✅ Timeline/feeds
  - ✅ Search

  Author

  Salman Tariq
  - GitHub: https://github.com/Salmantariq12
  - LinkedIn: https://www.linkedin.com/in/salman-tariq-47089592

  License

  MIT License

  ---
