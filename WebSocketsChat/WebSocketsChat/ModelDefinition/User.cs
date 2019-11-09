using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
