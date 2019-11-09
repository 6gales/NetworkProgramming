using System;
using System.Net.Http;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using RestChat.ModelDefinition;
using System.IO;
using System.Threading;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using WebSocketsChat.ModelDefinition;
using Newtonsoft.Json;

namespace RestChat.Server
{
	class Pair<T1, T2>
	{
		public T1 First { get; set; }
	
		public T2 Second { get; set; }
	}
		class Server
	{
		private int _userId = 0;
		private int _messageId = 0;
		private int _port;
		private ConcurrentDictionary<int, User> _users;
		private ConcurrentDictionary<string, int> _tokens;
		private ConcurrentDictionary<string, int> _usernames;
		private ConcurrentDictionary<int, Message> _messages;
		
		private Dictionary<WebSocket, Pair<int, Task>> _webSockets;

		private Server(int port)
		{
			_port = port;
			_messages = new ConcurrentDictionary<int, Message>();
			_tokens = new ConcurrentDictionary<string, int>();
			_users = new ConcurrentDictionary<int, User>();
			_usernames = new ConcurrentDictionary<string, int>();

			_webSockets = new Dictionary<WebSocket, Pair<int, Task>>();
		}

		private void HandleLogin(HttpListenerContext context)
		{
			LoginRequest request;
			try
			{
				request = RestMethods.ReadBody<LoginRequest>(context.Request);
			}
			catch (ArgumentException e)
			{
				RestMethods.WriteError(context.Response, HttpStatusCode.BadRequest, e.Message);
				return;
			}

			if (_usernames.TryGetValue(request.Username, out int id))
			{
				if (_users[id].Online)
				{
					context.Response.AppendHeader(HttpResponseHeader.WwwAuthenticate.ToString(), "Token realm = 'Username is already in use'");
					RestMethods.WriteError(context.Response, HttpStatusCode.Unauthorized, "username is already in use");
					return;
				}
				else
				{
					_users.TryRemove(id, out User val);
					_usernames.TryRemove(request.Username, out id);
					_tokens.TryRemove(_tokens.Where(tokenid => tokenid.Value == id).First().Key, out id);
				}
			}

			lock (this)
			{
				id = ++_userId;
			}
			string token = Convert.ToBase64String(Guid.NewGuid().ToByteArray()).Replace('=', '-');

			LoginResponse loginResponse = new LoginResponse
			{
				Username = request.Username,
				Id = id,
				Online = true,
				Token = token
			};

			lock (_usernames)
			{
				if (!_usernames.TryAdd(loginResponse.Username, id))
				{
					context.Response.AppendHeader(HttpResponseHeader.WwwAuthenticate.ToString(), "Token realm = 'Username is already in use'");
					RestMethods.WriteError(context.Response, HttpStatusCode.Unauthorized, "username is already in use");
				}
				_users.TryAdd(id, new User
				{
					Username = loginResponse.Username,
					Id = id,
					Online = true
				});
				_tokens.TryAdd(token, id);
			}

			SendToAllSubscribers(WebSocketJsonType.User, _users.Values);

			RestMethods.WriteToResponse(context.Response, loginResponse);
		}

		private void HandleLogout(HttpListenerContext context)
		{
			var token = RestMethods.GetToken(context.Request);

			if (!_tokens.TryRemove(token, out int id))
			{
				RestMethods.WriteError(context.Response, HttpStatusCode.Unauthorized, "Authorization failed");
				return;
			}
			_users.TryRemove(id, out User user);
			_usernames.TryRemove(user.Username, out id);

			SendToAllSubscribers(WebSocketJsonType.User, _users.Values);

			context.Response.StatusCode = (int)HttpStatusCode.NoContent;
			context.Response.OutputStream.Close();
		}

		private void SendToAllSubscribers<T>(WebSocketJsonType type, T data)
		{
			List<WebSocket> toRemove = new List<WebSocket>();

			string responseStr = JsonConvert.SerializeObject(data);
			byte[] buffer = Encoding.UTF8.GetBytes(responseStr);
			byte[] buffWithType = new byte[buffer.Length + 1];
			buffWithType[0] = (byte)type;
			//			Buffer.BlockCopy(buffer, 0, buffWithType, 1, buffer.Length);

			for (int i = 0; i < buffer.Length; i++)
			{
				buffWithType[i + 1] = buffer[i];
			}

			ArraySegment<byte> segment = new ArraySegment<byte>(buffWithType);

			lock (_webSockets)
			{
				foreach (var ws in _webSockets)
				{
					if (ws.Value.Second.IsCompleted)
					{
						if (_users.TryGetValue(ws.Value.First, out User user))
							user.Online = false;
						toRemove.Add(ws.Key);
					}
					else
					{
						ws.Key.SendAsync(segment, WebSocketMessageType.Binary, false, CancellationToken.None).Wait();
					}
				}

				foreach (var ws in toRemove)
				{
					_webSockets.Remove(ws);
				}
			}
		}

		private void AddMessage(Message message)
		{
			lock (_messages)
			{
				message.Id = ++_messageId;
			}
			_messages.TryAdd(message.Id, message);

			SendToAllSubscribers(WebSocketJsonType.Message, message);
		}

		private bool DeleteMessage(int id)
		{
			if (_messages.TryRemove(id, out Message val))
			{
				SendToAllSubscribers(WebSocketJsonType.Messages, _messages.Values);
				return true;
			}
			return false;
		}

		private bool GetMessagesByQuery(Dictionary<string, string> query, out IEnumerable<Message> messages)
		{
			messages = null;
			if (query == null || query.Count == 0)
			{
				messages = _messages.Values;
				return true;
			}

			int offset = 0;
			if (query.TryGetValue("offset", out string strOffset)
				&& !int.TryParse(strOffset, out offset))
			{
				return false;
			}

			bool fromEnd = false;
			if (query.TryGetValue("end", out string strEnd)
				&& !bool.TryParse(strEnd, out fromEnd))
			{
				return false;
			}

			if (query.TryGetValue("count", out string strCount)
				&& int.TryParse(strCount, out int count))
			{
				int num = 0, storedCount = 0;

				if (fromEnd)
				{
					offset = _messages.Count - count;
					if (offset < 0) offset = 0;
				}

				var listMessages = new List<Message>();
				foreach (var pair in _messages)
				{
					if (num++ >= offset && storedCount++ < count)
					{
						listMessages.Add(pair.Value);
					}
				}

				messages = listMessages;
				return true;
			}

			return false;
		}

		private bool GetUsersByQuery(Dictionary<string, string> query, out IEnumerable<User> users)
		{
			users = null;
			if (query == null || query.Count == 0)
			{
				users = _users.Values;
				return true;
			}

			query.TryGetValue("name", out string name);

			bool? online = null;
			if (query.TryGetValue("online", out string strOnline)
				&& bool.TryParse(strOnline, out bool bOnline))
			{
				online = bOnline;
			}

			users = _users.Values.Where(user =>
				(string.IsNullOrEmpty(name) || user.Username == name)
				&& (online != null ? online == user.Online : true));
			return true;
		}

		private bool VerifyToken(HttpListenerRequest request)
		{
			string token = RestMethods.GetToken(request);
			return _tokens.ContainsKey(token);
		}

		private void Subscribe(HttpListenerContext context)
		{
			if (!context.Request.IsWebSocketRequest)
			{
				RestMethods.WriteError(context.Response, HttpStatusCode.BadRequest, "not web socket request");
				return;
			}

			if (!RestMethods.ParseQuery(context.Request.Url.Query, out Dictionary<string, string> query)
				|| query == null || query.Count == 0 || !query.ContainsKey("token"))
			{
				RestMethods.WriteError(context.Response, HttpStatusCode.BadRequest, "missing token parameter");
				return;
			}

			var wsContext = context.AcceptWebSocketAsync(null).Result;
			if (!_tokens.TryGetValue(query["token"], out int userId))
			{
				RestMethods.WriteError(context.Response, HttpStatusCode.Unauthorized, "no match for token value");
				return;
			}

			lock (_webSockets)
			{
				var cancelletionTask = wsContext.WebSocket.ReceiveAsync(new ArraySegment<byte>(new byte[1024]), CancellationToken.None);
				_webSockets.Add(wsContext.WebSocket, new Pair<int, Task>
				{
					First = userId,
					Second = cancelletionTask
				});
			}
		}

		void Run()
		{
			Router router = new Router();

			RestMethods restMethods = new RestMethods(VerifyToken);

			router.AddHandler("/subscribe", HttpMethod.Get, Subscribe);

			router.AddHandler("/login", HttpMethod.Post, HandleLogin);
			router.AddHandler("/logout", HttpMethod.Post, HandleLogout);

			router.AddHandler("/users", HttpMethod.Get,
				(context) =>
				{
					restMethods.PerformGet<User>(context, GetUsersByQuery);
				}
			);
			router.AddHandler("/users/" + Router.IntegerUrlParameter, HttpMethod.Get,
				(context) => restMethods.PerformGetById<User>(context, _users.TryGetValue, 
							context.Request.Url.Segments[2])
			);

			router.AddHandler("/messages", HttpMethod.Post,
				(context) => restMethods.PerformPost<Message>(context, AddMessage)
			);
			router.AddHandler("/messages", HttpMethod.Get,
				(context) =>
				{
					restMethods.PerformGet<Message>(context, GetMessagesByQuery);
				}
			);
			router.AddHandler("/messages/" + Router.IntegerUrlParameter, HttpMethod.Get,
				(context) => restMethods.PerformGetById<Message>(context, _messages.TryGetValue,
							context.Request.Url.Segments[2])
			);
			router.AddHandler("/messages/" + Router.IntegerUrlParameter, HttpMethod.Delete,
				(context) => restMethods.PerformDelete(context,
				DeleteMessage, context.Request.Url.Segments[2])
			);

			using (HttpListener httpListener = new HttpListener())
			{
				httpListener.Prefixes.Add("http://localhost:" + _port + "/");
				httpListener.Start();

				while (true)
				{
					var context = httpListener.GetContext();
					HttpListenerRequest request = context.Request;
					Thread t = new Thread(() =>
					{
						if (router.TryGetHandler(request, out HandlerFunc handler))
						{
							handler(context);
						}
						else RestMethods.WriteError(context.Response, HttpStatusCode.BadRequest, "no handler provided");
					});
					t.Start();
				}
			}
		}

		static void Main(string[] args)
		{
			Server server = new Server(8888);
			server.Run();
		}
	}
}
