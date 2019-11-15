using System;
using System.Net.Http;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using RestChat.ModelDefinition;
using System.Threading;
using System.Collections.Concurrent;

namespace RestChat.Server
{
	class Server
	{
		private const int EventsToSkip = 3;
		private int _userId;
		private int _messageId;
		private readonly int _port;
		private ConcurrentDictionary<int, User> _users;
		private ConcurrentDictionary<string, int> _tokens;
		private ConcurrentDictionary<string, int> _usernames;
		private ConcurrentDictionary<int, Message> _messages;
		private ConcurrentDictionary<int, int> _eventsHandled;

		private EventWaitHandle _userEvent;
		private EventWaitHandle _messageEvent;

		private Server(int port)
		{
			_port = port;
			_messages = new ConcurrentDictionary<int, Message>();
			_tokens = new ConcurrentDictionary<string, int>();
			_users = new ConcurrentDictionary<int, User>();
			_usernames = new ConcurrentDictionary<string, int>();
			_eventsHandled = new ConcurrentDictionary<int, int>();
			_userEvent = new EventWaitHandle(false, EventResetMode.ManualReset);
			_messageEvent = new EventWaitHandle(false, EventResetMode.ManualReset);
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
					_users.TryRemove(id, out _);
					_usernames.TryRemove(request.Username, out id);
					_tokens.TryRemove(_tokens.First(tokenId => tokenId.Value == id).Key, out id);
				}
			}

			lock (this)
			{
				id = ++_userId;
			}
			var token = Convert.ToBase64String(Guid.NewGuid().ToByteArray());

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

			CountAndDecrement(_userEvent);

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

			CountAndDecrement(_userEvent);

			context.Response.StatusCode = (int)HttpStatusCode.NoContent;
			context.Response.OutputStream.Close();
		}

		private void SignEventAttendance(string token)
		{
			if (token == null) return;

			if (_tokens.TryGetValue(token, out int id))
			{
				_eventsHandled.AddOrUpdate(id, EventsToSkip, (key, val) => EventsToSkip);
			}
		}

		private void CountAndDecrement(EventWaitHandle eventWait)
		{
			eventWait.Set();
			eventWait.Reset();

			foreach (var pair in _eventsHandled)
			{
				if (pair.Value <= 0 && _users.TryGetValue(pair.Key, out User user))
				{
					user.Online = false;
				}
				else
				{
					_eventsHandled.TryUpdate(pair.Key, pair.Value - 1, pair.Value);
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

			CountAndDecrement(_messageEvent);
		}

		private bool DeleteMessage(int id)
		{
			return _messages.TryRemove(id, out _);
		}

		private bool GetMessagesByQuery(Dictionary<string, string> query, out IEnumerable<Message> messages)
		{
			messages = null;
			if (query == null || query.Count == 0)
			{
				_messageEvent.WaitOne();
				messages = new[] { _messages.Values.Last() };
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
				_userEvent.WaitOne();
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

		void Run()
		{
			Router router = new Router();

			RestMethods restMethods = new RestMethods(VerifyToken);

			router.AddHandler("/login", HttpMethod.Post, HandleLogin);
			router.AddHandler("/logout", HttpMethod.Post, HandleLogout);

			router.AddHandler("/users", HttpMethod.Get,
				(context) =>
				{
					SignEventAttendance(RestMethods.GetToken(context.Request));
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
					SignEventAttendance(RestMethods.GetToken(context.Request));
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
							handler(context);
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
	}
}
