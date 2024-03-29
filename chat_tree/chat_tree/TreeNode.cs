﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using ChatTree;
using ChatTree.MessageUtils;

namespace СhatTree
{
	class TreeNode
	{
		private readonly string _name;
		private readonly int _port;
		private readonly int _lossRate;
		private readonly long _resendTimeout = 1500L;
		private readonly long _maxUnavilableTimeout = 10000L;

		private bool _notExited = true;

		private IPEndPoint _parentIP = null;
		private IPEndPoint _reserveNode = null;
		private IPEndPoint _childsReserve;
		private HashSet<IPEndPoint> _childs;

		private HashSet<Guid> _messageHistory;

		private delegate void Command();
		private Dictionary<string, Command> _consoleCommands;

		public TreeNode(string name, int loss, int port) : this(name, loss, port, null, 0) { }

		public TreeNode(string name, int lossRate, int port, string parentAddr, int parentPort)
		{
			_name = name;
			_lossRate = lossRate;
			_port = port;

			if (parentAddr != null)
			{
				_parentIP = new IPEndPoint(IPAddress.Parse(parentAddr), parentPort);
			}

			_childsReserve = _parentIP;

			_childs = new HashSet<IPEndPoint>();
			_messageHistory = new HashSet<Guid>();

			_consoleCommands = new Dictionary<string, Command>()
			{
				["/parent"] = () => Console.WriteLine("Parent: {0}", _parentIP),
				["/childs"] = () => Console.WriteLine("Childs: {" + string.Join(", ", _childs) + "}"),
				["/exit"] = () => _notExited = false,
			};
		}

		private async Task<string> GetLineAsync()
		{
			return await Task.Run(() => Console.ReadLine());
		}

		private void HandleUserInput(string line, MessageManager manager)
		{
			if (_consoleCommands.ContainsKey(line))
			{
				_consoleCommands[line].Invoke();
				return;
			}

			DataMessage message = new DataMessage(_name, line);
			manager.SendToAll(message);
		}

		public void Run()
		{
			using (UdpClient udpClient = new UdpClient(_port))
			{
				MessageManager manager = new MessageManager(udpClient, _name, _resendTimeout);

				if (_parentIP != null)
				{
					manager.ConnectTo(_parentIP);
				}

				udpClient.Client.ReceiveTimeout = 500;
				
				Task<string> readLine = GetLineAsync();

				while (_notExited)
				{
					long start = DateTimeOffset.Now.ToUnixTimeMilliseconds();

					while (DateTimeOffset.Now.ToUnixTimeMilliseconds() - start < _resendTimeout)
					{
						if (readLine.IsCompleted)
						{
							HandleUserInput(readLine.Result, manager);
							readLine = Task.Run(() => Console.ReadLine());
						}

						Message message = manager.TryReceiveMessage(_lossRate, out IPEndPoint sender);
						if (message == null)
							continue;

						AnswerMessage(manager, message, sender);
					}
					
					RemoveAndReelectNodes(manager);
					manager.Resend();
					manager.SendToAll(new Message(_name, ContentType.HealthCheck));
				}
			}
		}
		
		void AnswerMessage(MessageManager manager, Message message, IPEndPoint sender)
		{
			switch (message.Type)
			{
				case ContentType.ConnectionRequest: //accept connection
					if (!_childs.Contains(sender))
					{
						_childs.Add(sender);
						manager.Add(sender);

						if (_childsReserve != null)
						{
							manager.SendToOne(new ReserveNodeMessage(_name, _childsReserve), sender);
						}
						else
						{
							//if node has no parent, elect first child as reserve node
							_childsReserve = _childs.First();
						}
					}
					goto default;

				case ContentType.ReceptionConfirmation:
					Guid confirmedID = ((ConfirmationMessage)message).ConfirmedGuid;
					manager.MessageConfirmed(sender, confirmedID);
					break;

				case ContentType.Data: //send to all, if received first time
					if (!_messageHistory.Contains(message.GuidProperty))
					{
						_messageHistory.Add(message.GuidProperty);
						Console.WriteLine("@{0}: {1}", message.Name, ((DataMessage)message).Data);
						manager.SendToAllExclude(message, sender);
					}
					goto default;

				case ContentType.ReserveNode: //update reserve node
					_reserveNode = ((ReserveNodeMessage)message).ReserveNode.GetIPEndPoint();
					goto default;

				default:
					manager.ConfirmReception(message.GuidProperty, sender);
					break;
			}

		}
		
		private void RemoveAndReelectNodes(MessageManager manager)
		{
			var itemsToRemove = manager.GetUnavailableNodes(_maxUnavilableTimeout);

			foreach (var item in itemsToRemove)
			{
				Console.WriteLine(">>> " + item.Key + " is unavailable");
				manager.RemoveNode(item.Key);
				if (item.Key.Equals(_parentIP))
				{
					_parentIP = _reserveNode;
					_reserveNode = null;

					if (_parentIP != null)
						manager.ConnectTo(_parentIP);
				}
				else
				{
					_childs.Remove(item.Key);
				}

				if (item.Key.Equals(_childsReserve))
				{
					_childsReserve = _parentIP ?? (_childs.Count > 0 ? _childs.First() : null);

					if (_childsReserve == null)
						continue;
				
					var reserveNodeMessage = new ReserveNodeMessage(_name, _childsReserve);
					manager.SendToAllExclude(reserveNodeMessage, _childsReserve);
				}
			}
		}
	}
}