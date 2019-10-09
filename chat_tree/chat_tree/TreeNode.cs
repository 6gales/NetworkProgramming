using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;
using ChatTree.MessageUtils;

namespace СhatTree
{
	class TreeNode
	{
		private string _name;
		private int _port;
		private int _lossRate;
		private BinaryFormatter _formatter;
		private IPEndPoint _parentIP = null;
		private HashSet<IPEndPoint> _childs;

		public TreeNode(string name, int loss, int port) : this(name, loss, port, null, 0) { }

		public TreeNode(string name, int lossRate, int port, string parentAddr, int parentPort)
		{
			_formatter = new BinaryFormatter();
			_childs = new HashSet<IPEndPoint>();
			_name = name;
			_lossRate = lossRate;
			_port = port;
			if (parentAddr != null)
				_parentIP = new IPEndPoint(IPAddress.Parse(parentAddr), parentPort);
		}

		private void ConfirmReception(Guid guid, UdpClient udpClient, IPEndPoint endPoint)
		{
			Message<Guid> confirmation = new Message<Guid>(_name, ContentType.ReceptionConfirmation, guid);
			SendMessage(confirmation, udpClient, endPoint);
		}

		private byte[] SerializeMessage<T>(Message<T> message)
		{
			MemoryStream data = new MemoryStream();
			_formatter.Serialize(data, message);
			return data.ToArray();
		}
		//private async Task<string> GetInputAsync()
		//{
		//	return Task.Run(() => Console.ReadLine());
		//}
		private void SendMessage<T>(Message<T> message, UdpClient udpClient, IPEndPoint endPoint)
		{
			byte[] bytes = SerializeMessage(message);
			udpClient.Send(bytes, bytes.Length, endPoint);
		}

		public void Run()
		{
			int maxFailedAttempts = 10;
			IPEndPoint reserveNode = null,
				childsReserve = _parentIP;
			Random rng = new Random();
			HashSet<Guid> messageHistory = new HashSet<Guid>();
			var endPointsQueues = new Dictionary<IPEndPoint, QueuedMessages>();
			if (_parentIP != null)
				endPointsQueues.Add(_parentIP, new QueuedMessages());

			using (UdpClient udpClient = new UdpClient(_port))
			{
				udpClient.Client.ReceiveTimeout = 300;
				IPEndPoint remoteIpEndPoint = null;
				Task<string> readLine;

				while (true)
				{
					readLine = Task.Run(() => Console.ReadLine());

					if (readLine.IsCompleted)
					{
						Message<string> message = new Message<string>(_name, ContentType.Data, readLine.Result);

						byte[] bytes = SerializeMessage(message);
						foreach (var ipQueuedMessages in endPointsQueues)
						{
							ipQueuedMessages.Value.Add(message.GuidProperty, bytes);
						}
						readLine = Task.Run(() => Console.ReadLine());
					}

					try
					{
						byte[] receivedBytes = udpClient.Receive(ref remoteIpEndPoint);

						if (rng.Next(100) < _lossRate)
						{
							Console.Out.WriteLine("Lost message from {1}", remoteIpEndPoint);
							continue;
						}

						Message<object> message = (Message<object>)_formatter.Deserialize(new MemoryStream(receivedBytes));

						switch (message.Type)
						{
							case ContentType.ConnectionRequest:
								if (!_childs.Contains(remoteIpEndPoint))
								{
									_childs.Add(remoteIpEndPoint);
									endPointsQueues.Add(remoteIpEndPoint, new QueuedMessages());
								}
								ConfirmReception(message.GuidProperty, udpClient, remoteIpEndPoint);

								if (childsReserve != null)
								{
									Message<IPEndPoint> reserveNodeMessage = new Message<IPEndPoint>(_name, ContentType.ReserveNode, childsReserve);
									byte[] data = SerializeMessage(reserveNodeMessage);
									endPointsQueues[remoteIpEndPoint].Add(reserveNodeMessage.GuidProperty, data);
								}
								else if (_childs.Count > 0)
								{
									childsReserve = _childs.First();
								}

								break;

							case ContentType.ReceptionConfirmation:
								Guid confirmedID = (Guid)message.Content;
								endPointsQueues[remoteIpEndPoint].Remove(confirmedID);
								break;

							case ContentType.Data:
								if (!messageHistory.Contains(message.GuidProperty))
								{
									messageHistory.Add(message.GuidProperty);
									Console.WriteLine(message);
									foreach (var ipQueuedMessages in endPointsQueues)
									{
										if (ipQueuedMessages.Key != remoteIpEndPoint)
											ipQueuedMessages.Value.Add(message.GuidProperty, receivedBytes);
									}
								}
								ConfirmReception(message.GuidProperty, udpClient, remoteIpEndPoint);
								break;

							case ContentType.ReserveNode:
								reserveNode = (IPEndPoint)message.Content;
								ConfirmReception(message.GuidProperty, udpClient, remoteIpEndPoint);
								break;
						}

						endPointsQueues[remoteIpEndPoint].DiscardAttempts();
					}
					catch (SocketException) { } //ignore timeout

					var itemsToRemove = endPointsQueues.Where(pair => pair.Value.UnavailableFor > maxFailedAttempts).ToArray();
					foreach (var item in itemsToRemove)
					{
						endPointsQueues.Remove(item.Key);
						if (_parentIP != null && _parentIP == item.Key)
						{
							_parentIP = reserveNode;
							Message<object> message = new Message<object>(_name, ContentType.ConnectionRequest, null);
							byte[] bytes = SerializeMessage(message);
							endPointsQueues.Add(reserveNode, new QueuedMessages());
							endPointsQueues[reserveNode].Add(message.GuidProperty, bytes);
						}
						else _childs.Remove(item.Key);

						if (childsReserve == item.Key)
						{
							if (_parentIP == null)
							{
								childsReserve = _childs.First();
							}
							else childsReserve = _parentIP;

							Message<IPEndPoint> reserveNodeMessage = new Message<IPEndPoint>(_name, ContentType.ReserveNode, childsReserve);
							byte[] data = SerializeMessage(reserveNodeMessage);
							foreach (var child in _childs)
							{
								if (child != childsReserve)
									endPointsQueues[child].Add(reserveNodeMessage.GuidProperty, data);
							}
						}
					}

					foreach (var ipQueuedMessages in endPointsQueues)
					{
						ipQueuedMessages.Value.SendAll(ipQueuedMessages.Key, udpClient);
					}
				}
			}
		}
	}
}
