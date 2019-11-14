using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using WebSocketsChat.ModelDefinition;

namespace WebSocketsChat.Server
{
	class Server
	{
		private static readonly char[] Padding = { '=' };

		private int _userId;
		private int _messageId;
		private readonly int _port;
		private readonly ConcurrentDictionary<int, User> _users;
		private readonly ConcurrentDictionary<string, int> _tokens;
		private readonly ConcurrentDictionary<string, int> _usernames;
		private readonly ConcurrentDictionary<int, Message> _messages;
		
		private readonly Dictionary<WebSocket, Pair<int, Task>> _webSockets;

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

				_users.TryRemove(id, out _);
				_usernames.TryRemove(request.Username, out id);
				_tokens.TryRemove(_tokens.First(tokenId => tokenId.Value == id).Key, out id);
			}

			lock (this)
			{
				id = ++_userId;
			}
			
			var token = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
				.TrimEnd(Padding)
				.Replace('+', '-')
				.Replace('/', '_');

			var loginResponse = new LoginResponse
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

			if (!_tokens.TryRemove(token, out var id))
			{
				RestMethods.WriteError(context.Response, HttpStatusCode.Unauthorized, "Authorization failed");
				return;
			}
			_users.TryRemove(id, out var user);
			_usernames.TryRemove(user.Username, out id);

			SendToAllSubscribers(WebSocketJsonType.DeletedResource, new DeletedItem
			{
				Item = user
			});

			context.Response.StatusCode = (int)HttpStatusCode.NoContent;
			context.Response.OutputStream.Close();
		}

		private void SendToAllSubscribers<T>(WebSocketJsonType type, T data)
		{
			var wsToRemove = new List<WebSocket>();

			var responseStr = JsonConvert.SerializeObject(data);
			var buffer = Encoding.UTF8.GetBytes(responseStr);
			var buffWithType = new byte[buffer.Length + 1];
			buffWithType[0] = (byte)type;
			Buffer.BlockCopy(buffer, 0, buffWithType, 1, buffer.Length);

			var segment = new ArraySegment<byte>(buffWithType);

			lock (_webSockets)
			{
				foreach (var ws in _webSockets)
				{
					if (ws.Value.Second.IsCompleted)
					{
						if (_users.TryGetValue(ws.Value.First, out var user))
							user.Online = false;
						wsToRemove.Add(ws.Key);
					}
					else
					{
						ws.Key.SendAsync(segment, WebSocketMessageType.Binary, false, CancellationToken.None).Wait();
					}
				}

				foreach (var ws in wsToRemove)
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
			if (!_messages.TryRemove(id, out var message))
			{
				return false;
			}
			SendToAllSubscribers(WebSocketJsonType.DeletedResource, new DeletedItem
			{
				Item = message
			});
			return true;
		}

		private bool GetMessagesByQuery(Dictionary<string, string> query, out IEnumerable<Message> messages)
		{
			messages = null;
			if (query == null || query.Count == 0)
			{
				messages = _messages.Values;
				return true;
			}

			var offset = 0;
			if (query.TryGetValue("offset", out var strOffset)
				&& !int.TryParse(strOffset, out offset))
			{
				return false;
			}

			var fromEnd = false;
			if (query.TryGetValue("end", out var strEnd)
				&& !bool.TryParse(strEnd, out fromEnd))
			{
				return false;
			}

			if (!query.TryGetValue("count", out var strCount)
			    || !int.TryParse(strCount, out var count))
			{
				return false;
			}

			int num = 0, storedCount = 0;

			if (fromEnd)
			{
				offset = _messages.Count - count;
				if (offset < 0) offset = 0;
			}

			messages = (from pair in _messages
				where num++ >= offset && storedCount++ < count select pair.Value).ToList();
			return true;
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
				&& (online == null || online == user.Online));
			return true;
		}

		private bool VerifyToken(HttpListenerRequest request)
		{
			var token = RestMethods.GetToken(request);
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
				var cancellationTask = wsContext.WebSocket.ReceiveAsync(new ArraySegment<byte>(new byte[1024]), CancellationToken.None);
				_webSockets.Add(wsContext.WebSocket, new Pair<int, Task>
				{
					First = userId,
					Second = cancellationTask
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
					var request = context.Request;
					var t = new Thread(() =>
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
			if (args == null || args.Length < 1 || !int.TryParse(args[0], out var port))
			{
				port = 8888;
			}
			Server server = new Server(port);
			server.Run();
		}

		class Pair<T1, T2>
		{
			public T1 First { get; set; }

			public T2 Second { get; set; }
		}
	}
}
