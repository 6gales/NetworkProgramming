using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RestChat.ModelDefinition
{
	class Error
	{
		[JsonProperty("message")]
		public string Message { get; set; }
	}
}
