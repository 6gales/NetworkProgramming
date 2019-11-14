using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace WebSocketsChat.ModelDefinition
{
	class DeletedItem
	{
		[JsonProperty("item")]
		public IIdentifiedResource Item { get; set; }
	}
}
