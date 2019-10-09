using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ChatTree.MessageUtils
{
	class QueuedMessages
	{
		public int UnavailableFor { get; private set; }

		Dictionary<Guid, byte[]> _messageToDeliver;

		public QueuedMessages()
		{
			UnavailableFor = 0;
			_messageToDeliver = new Dictionary<Guid, byte[]>();
		}

		public void DiscardAttempts() => UnavailableFor = 0;

		public void Add(Guid guid, byte[] buffer) => _messageToDeliver.Add(guid, buffer);

		public void Remove(Guid guid) => _messageToDeliver.Remove(guid);

		public void SendAll(IPEndPoint receiver, UdpClient udpClient)
		{
			if (_messageToDeliver.Count > 0)
				UnavailableFor++;

			foreach (var message in _messageToDeliver)
			{
				udpClient.Send(message.Value, message.Value.Length, receiver);
			}
		}
	}
}
