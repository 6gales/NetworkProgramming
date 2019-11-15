using Newtonsoft.Json;

namespace WebSocketsChat.ModelDefinition
{
	class User : IdentifiedResource
	{
		[JsonProperty("username")]
		public string Username { get; set; }

		[JsonProperty("online")]
		public bool Online { get; set; }

		public override string ToString()
		{
			return $"({Id})<{Username}>: {(Online ? "online" : "offline")}";
		}
	}
}
