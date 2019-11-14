using Newtonsoft.Json;

namespace WebSocketsChat.ModelDefinition
{
	class Error
	{
		[JsonProperty("message")]
		public string Message { get; set; }
	}
}
