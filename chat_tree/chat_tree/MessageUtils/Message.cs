using System;
using System.Net;

namespace ChatTree.MessageUtils
{
	public enum ContentType { ConnectionRequest, Data, ReserveNode, ReceptionConfirmation }

	[Serializable]
	class Message
	{
		public string Name { get; }

		public Guid GuidProperty { get; }

		public ContentType Type { get; }

		public Message(string name, ContentType type)
		{
			Name = name;
			Type = type;
			GuidProperty = Guid.NewGuid();
		}

		public override string ToString()
		{
			return "{" + GuidProperty + "};<Type:" + Type.ToString() + ">;@" + Name;
		}
	}

	[Serializable]
	class ConfirmationMessage : Message
	{
		public Guid ConfirmedGuid { get; }

		public ConfirmationMessage(string name, Guid confirmationID) : base(name, ContentType.ReceptionConfirmation)
		{
			ConfirmedGuid = confirmationID;
		}

		public override string ToString()
		{
			return base.ToString() + ":" + ConfirmedGuid;
		}
	}

	[Serializable]
	class DataMessage : Message
	{
		public string Data { get; }

		public DataMessage(string name, string data) : base(name, ContentType.Data)
		{
			Data = data;
		}

		public override string ToString()
		{
			return base.ToString() + ":" + Data;
		}
	}

	[Serializable]
	class ReserveNodeMessage : Message
	{
		public IPEndPointWrapper ReserveNode { get; }

		public ReserveNodeMessage(string name, IPEndPoint endPoint) : base(name, ContentType.ReserveNode)
		{
			ReserveNode = new IPEndPointWrapper(endPoint);
		}

		public override string ToString()
		{
			return base.ToString() + ":" + ReserveNode.GetIPEndPoint();
		}
	}
}