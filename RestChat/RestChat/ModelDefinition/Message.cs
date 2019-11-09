using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RestChat.ModelDefinition
{
	class Message
	{
		[JsonProperty("id")]
		public int Id { get; set; }

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
