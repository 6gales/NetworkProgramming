using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace ChatTree.MessageUtils
{
	class QueuedMessages
	{
		class DataSendingTime
		{
			internal long Millis { get; set; }

			internal byte[] Data { get; set; }
		}

		public long LastSeenMillis { get; private set; }
		private readonly long _resendTimeout;

		Dictionary<Guid, DataSendingTime> _messageToDeliver;

		public QueuedMessages(long resendTimeout)
		{
			_resendTimeout = resendTimeout;
			LastSeenMillis = DateTimeOffset.Now.ToUnixTimeMilliseconds();
			_messageToDeliver = new Dictionary<Guid, DataSendingTime>();
		}

		public void UpdateLastSeen() => LastSeenMillis = DateTimeOffset.Now.ToUnixTimeMilliseconds();

		public void Add(Guid guid, byte[] buffer) => _messageToDeliver.Add(guid, new DataSendingTime { Millis= 0, Data= buffer });

		public void Remove(Guid guid) => _messageToDeliver.Remove(guid);

		public void ResendAll(IPEndPoint receiver, UdpClient udpClient)
		{
			long now = DateTimeOffset.Now.ToUnixTimeMilliseconds();

			foreach (var rawMessage in _messageToDeliver
				.Where(rawMessage  => now - rawMessage.Value.Millis > _resendTimeout))
			{
				rawMessage.Value.Millis = now;
				udpClient.Send(rawMessage.Value.Data, rawMessage.Value.Data.Length, receiver);
			}
		}
	}
}
