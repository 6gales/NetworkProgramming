using ChatTree.MessageUtils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;

namespace ChatTree
{
	class MessageManager
	{
		private readonly string _name;
		private UdpClient _udpClient;
		private Dictionary<IPEndPoint, QueuedMessages> _endPointsQueues;
		private BinaryFormatter _formatter;
		private Random _rng;

		private readonly long _resendTimeout;

		public MessageManager(UdpClient udpClient, string name, long resendTimeout)
		{
			_resendTimeout = resendTimeout;
			_name = name;
			_udpClient = udpClient;
			_rng = new Random();
			_formatter = new BinaryFormatter();
			_endPointsQueues = new Dictionary<IPEndPoint, QueuedMessages>();
		}

		private byte[] SerializeMessage(Message message)
		{
			MemoryStream data = new MemoryStream();
			_formatter.Serialize(data, message);
			return data.ToArray();
		}

		public void ConnectTo(IPEndPoint endPoint)
		{
			_endPointsQueues.Add(endPoint, new QueuedMessages(_resendTimeout));
			SendToOne(new Message(_name, ContentType.ConnectionRequest), endPoint);
		}

		public void SendToOne(Message message, IPEndPoint receiver)
		{
			byte[] buffer = SerializeMessage(message);

			_udpClient.Send(buffer, buffer.Length, receiver);
			_endPointsQueues[receiver].Add(message.GuidProperty, buffer);
		}

		public void SendToAll(Message message)
		{
			byte[] buffer = SerializeMessage(message);

			foreach (var ipQueuedMessages in _endPointsQueues)
			{
				_udpClient.Send(buffer, buffer.Length, ipQueuedMessages.Key);
				ipQueuedMessages.Value.Add(message.GuidProperty, buffer);
			}
		}

		public void ConfirmReception(Guid guid, IPEndPoint endPoint)
		{
			ConfirmationMessage confirmation = new ConfirmationMessage(_name, guid);
			byte[] bytes = SerializeMessage(confirmation);
			_udpClient.Send(bytes, bytes.Length, endPoint);
		}

		public void SendToAllExclude(Message message, IPEndPoint excluded)
		{
			byte[] buffer = SerializeMessage(message);

			foreach (var ipQueuedMessages in _endPointsQueues.Where(ip => ip.Key != excluded))
			{
				_udpClient.Send(buffer, buffer.Length, ipQueuedMessages.Key);
				ipQueuedMessages.Value.Add(message.GuidProperty, buffer);
			}
		}

		public IEnumerable<KeyValuePair<IPEndPoint, QueuedMessages>> GetUnavailableNodes(long maxUnavilableTimeout)
		{
			long now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
			return _endPointsQueues.Where(pair => 
				now - pair.Value.LastSeenMillis > maxUnavilableTimeout && pair.Value.Count() > 0);
		}

		public void RemoveNode(IPEndPoint endPoint)
		{
			_endPointsQueues.Remove(endPoint);
		}

		public void Resend()
		{
			foreach (var ipQueuedMessages in _endPointsQueues)
			{
				ipQueuedMessages.Value.ResendAll(ipQueuedMessages.Key, _udpClient);
			}
		}

		public void Add(IPEndPoint endPoint)
		{
			_endPointsQueues.Add(endPoint, new QueuedMessages(_resendTimeout));
		}

		public void MessageConfirmed(IPEndPoint endPoint, Guid confirmedID)
		{
			_endPointsQueues[endPoint].Remove(confirmedID);
		}

		public Message TryReceiveMessage(int lossRate, out IPEndPoint sender)
		{
			sender = null;
			try
			{
				byte[] receivedBytes = _udpClient.Receive(ref sender);

				if (_rng.Next(100) < lossRate)
				{
					Console.WriteLine(">>> Lost message from " + sender);
					return null;
				}

				Message message = (Message)_formatter.Deserialize(new MemoryStream(receivedBytes));
				
				if (_endPointsQueues.ContainsKey(sender))
					_endPointsQueues[sender].UpdateLastSeen();
				
				return message;
			}
			catch (SocketException) { } //ignore timeout

			return null;
		}
	}
}
