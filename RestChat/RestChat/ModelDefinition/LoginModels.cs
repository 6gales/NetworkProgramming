using Newtonsoft.Json;

namespace RestChat.ModelDefinition
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
