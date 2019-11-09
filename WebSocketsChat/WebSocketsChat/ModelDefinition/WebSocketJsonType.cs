using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebSocketsChat.ModelDefinition
{
	enum WebSocketJsonType : byte
	{
		Message,
		Messages,
		User
	}
}
