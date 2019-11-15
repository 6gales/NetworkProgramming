using Newtonsoft.Json;

namespace WebSocketsChat.ModelDefinition
{
	class Message : IdentifiedResource
	{
		[JsonProperty("message")]
		public string Data { get; set; }

		[JsonProperty("author")]
		public string Author { get; set; }

		public override string ToString()
		{
			return $"#{Id}<@{Author}>: {Data}";
		}
	}
}
