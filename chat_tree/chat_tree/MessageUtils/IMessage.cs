using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatTree.MessageUtils
{
	interface IMessage<out T>
	{
		string Name { get; }

		Guid GuidProperty { get; }

		ContentType Type { get; }

		T Content { get; }
	}
}
