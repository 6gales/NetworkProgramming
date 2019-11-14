using Newtonsoft.Json;

namespace WebSocketsChat.ModelDefinition
{
	class LoginRequest
	{
		[JsonProperty("username")]
		public string Username { get; set; }
	}

	class LoginResponse : User
	{
		[JsonProperty("token")]
		public string Token { get; set; }
	}
}
