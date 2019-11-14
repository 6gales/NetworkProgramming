using Newtonsoft.Json;

namespace RestChat.ModelDefinition
{
	class User
	{
		[JsonProperty("id")]
		public int Id { get; set; }

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
