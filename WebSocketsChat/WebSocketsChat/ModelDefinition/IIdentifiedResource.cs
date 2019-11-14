using Newtonsoft.Json;

namespace WebSocketsChat.ModelDefinition
{
	interface IIdentifiedResource
	{
		[JsonProperty("id")]
		int Id { get; set; }
	}

	class IdentifiedResource : IIdentifiedResource
	{
		[JsonProperty("id")]
		public int Id { get; set; }

		public override bool Equals(object obj)
		{
			if (obj is IIdentifiedResource idr)
			{
				return Id == idr.Id;
			}

			return false;
		}

		public override int GetHashCode()
		{
			return Id;
		}
	}
}
