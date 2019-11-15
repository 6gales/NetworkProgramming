using Newtonsoft.Json;

namespace WebSocketsChat.ModelDefinition
{
	class DeletedItem
	{
		[JsonProperty("item")]
		public IdentifiedResource Item { get; set; }

		public WebSocketJsonType Type { get; set; }
	}
}
