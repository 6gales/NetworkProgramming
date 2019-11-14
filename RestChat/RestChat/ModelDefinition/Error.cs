using Newtonsoft.Json;

namespace RestChat.ModelDefinition
{
	class Error
	{
		[JsonProperty("message")]
		public string Message { get; set; }
	}
}
