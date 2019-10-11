using System;

namespace ChatTree.MessageUtils
{
	public enum ContentType { ConnectionRequest, Data, ReserveNode, ReceptionConfirmation }

	[Serializable]
	class Message<T> : IMessage<T>
	{
		public string Name { get; }

		public Guid GuidProperty { get; }

		public ContentType Type { get; }

		public T Content { get; }

		public Message(string name, ContentType type, T content)
		{
			Name = name;
			Content = content;
			Type = type;
			GuidProperty = Guid.NewGuid();
		}

		public override string ToString()
		{
			return "<" + GuidProperty + ">:Type:" + Type.ToString() + ":@" + Name + ": " + Content;
		}
	}
}