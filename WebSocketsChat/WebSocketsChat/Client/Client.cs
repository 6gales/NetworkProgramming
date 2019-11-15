using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using WebSocketsChat.ModelDefinition;

namespace WebSocketsChat.Client
{
	class Client
	{
		private bool _notExited = true;
		private readonly string _server;
		private readonly string _httpAddress;

		private delegate void Command(string parameters);
		private readonly Dictionary<string, Command> _consoleCommands;

		private List<Message> _messages;
		private List<User> _users;

		HttpClient _httpClient;

		Client(string server, int port)
		{
			_server = server + ":" + port + "/";
			_httpAddress = "http://" + _server;

			_consoleCommands = new Dictionary<string, Command>()
			{
				["/help"] = _ => Console.WriteLine("Commands: " + string.Join("\n\t", _consoleCommands.Keys)),
				["/list"] = _ => ListUsers(),
				["/user"] = GetUser,
				["/delete"] = DeleteMessage,
				["/logout"] = _ => _notExited = false,
				["/messages"] = _ => ShowMessages(true)
			};
		}

		private static T GetFromResponse<T>(HttpResponseMessage response) 
			=> JsonConvert.DeserializeObject<T>(response.Content.ReadAsStringAsync().Result);

		private void GetUser(string param)
		{
			string[] parsed = param.Split(' ');
			if (_httpClient == null || parsed.Length < 2)
			{
				Console.WriteLine(">>> Invalid command arguments");
				return;
			}

			var response = _httpClient.GetAsync(_httpAddress + "/users/" + parsed[1]).Result;

			if (response.StatusCode == System.Net.HttpStatusCode.OK)
			{
				var user = GetFromResponse<User>(response);
				Console.WriteLine(">>> " + user);
			}
			else
			{
				var error = GetFromResponse<Error>(response);
				Console.WriteLine(">>> " + error.Message);
			}
		}

		private void ShowMessages(bool clearConsole)
		{
			if (clearConsole)
				Console.Clear();

			foreach (var message in _messages)
			{
				Console.WriteLine(message);
			}
		}

		private void ListUsers()
		{
			Console.WriteLine("Users:");
			foreach (var user in _users)
			{
				Console.WriteLine("\t" + user);
			}
		}

		private void DeleteMessage(string param)
		{
			string[] parsed = param.Split(' ');
			if (_httpClient == null || parsed.Length < 2)
			{
				Console.WriteLine(">>> Invalid command arguments");
				return;
			}

			var response = _httpClient.DeleteAsync(_httpAddress + "/messages/" + parsed[1]).Result;

			if (response.StatusCode != System.Net.HttpStatusCode.NoContent)
			{
				var error = GetFromResponse<Error>(response);
				Console.WriteLine(">>> " + error.Message);
			}
			else Console.WriteLine(">>> Message deleted");
		}

		private static async Task<string> GetLineAsync() => await Task.Run(() => Console.ReadLine());

		private Task<HttpResponseMessage> PostTo<T>(string url, T content)
		{
			HttpContent httpContent = new StringContent(JsonConvert.SerializeObject(content), Encoding.UTF8, "application/json");
			return _httpClient.PostAsync(_httpAddress + url, httpContent);
		}

		private void HandleMessage(byte[] bytes, int receiveLen)
		{
			var data = Encoding.UTF8.GetString(bytes, 1, receiveLen - 1);

			switch (bytes[0])
			{
				case (byte)WebSocketJsonType.Message:
					var message = JsonConvert.DeserializeObject<Message>(data);
					_messages.Add(message);
					Console.WriteLine(message);
					break;

				case (byte)WebSocketJsonType.Messages:
					_messages = JsonConvert.DeserializeObject<List<Message>>(data);
					ShowMessages(true);
					break;

				case (byte)WebSocketJsonType.User:
					_users.Add(JsonConvert.DeserializeObject<User>(data));
					ListUsers();
					break;

				case (byte)WebSocketJsonType.Users:
					_users = JsonConvert.DeserializeObject<List<User>>(data);
					ListUsers();
					break;

				default:
					var deleted = JsonConvert.DeserializeObject<DeletedItem>(data);

					if (deleted.Type == WebSocketJsonType.User)
					{
						_users.First(deleted.Item.Equals).Online = false;
						ListUsers();
					}
					else
					{
						_messages.Remove(_messages.First(deleted.Item.Equals));
						ShowMessages(true);
					}
					break;
			}
		}

		private LoginResponse LogIn()
		{
			HttpResponseMessage resp = null;
			Console.Write("Enter your nickname: ");
			do
			{
				if (resp != null)
					Console.Write("Sorry, user with this nickname is already exists. \nPlease, choose another one: ");
				LoginRequest loginRequest = new LoginRequest { Username = Console.ReadLine() };
				
				resp = PostTo("/login", loginRequest).Result;
				
			} while (resp.StatusCode != System.Net.HttpStatusCode.OK);

			var login = JsonConvert.DeserializeObject<LoginResponse>(resp.Content.ReadAsStringAsync().Result);

			_httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);

			resp = _httpClient.GetAsync(_httpAddress + "/messages?count=30&end=true").Result;
			_messages = JsonConvert.DeserializeObject<List<Message>>(resp.Content.ReadAsStringAsync().Result);
			ShowMessages(false);

			return login;
		}

		void Run()
		{
			using (HttpClient httpClient = new HttpClient())
			using (ClientWebSocket webSocket = new ClientWebSocket())
			{
				httpClient.Timeout = Timeout.InfiniteTimeSpan;
				_httpClient = httpClient;

				var login = LogIn();

				webSocket.ConnectAsync(new Uri($"ws://{_server}/subscribe?token={login.Token}"), CancellationToken.None).Wait();
				var bytes = new byte[2048];
				var byteSegment = new ArraySegment<byte>(bytes);


				Task<WebSocketReceiveResult> webSocketResult = webSocket.ReceiveAsync(byteSegment, CancellationToken.None);
				webSocketResult.Wait();
				HandleMessage(bytes, webSocketResult.Result.Count);
				webSocketResult = webSocket.ReceiveAsync(byteSegment, CancellationToken.None);

				Task<string> getLine = GetLineAsync();

				while (_notExited)
				{
					if (getLine.IsCompleted)
					{
						var line = getLine.Result;
						if (!string.IsNullOrEmpty(line))
						{
							var pos = line.IndexOf(" ", StringComparison.Ordinal);
							var command = (pos > 0 ? line.Substring(0, pos) : line);

							if (_consoleCommands.TryGetValue(command, out Command cmd))
							{
								cmd.Invoke(line);
							}
							else
							{
								Console.SetCursorPosition(Console.CursorLeft, Console.CursorTop - 1);
								PostTo("/messages", new Message { Data = line, Author = login.Username });
							}
						}
						getLine = GetLineAsync();
					}

					if (webSocketResult.IsCompleted)
					{
						HandleMessage(bytes, webSocketResult.Result.Count);
						webSocketResult = webSocket.ReceiveAsync(byteSegment, CancellationToken.None);
					}

					Thread.Sleep(500);
				}

				httpClient.PostAsync(_httpAddress + "/logout", new StringContent("")).Wait();
				webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "logout", CancellationToken.None);
			}
		}

		static void Main(string[] args)
		{
			if (args.Length < 1)
			{
				Console.WriteLine("Usage: <server address> <port>");
				return;
			}
			if (args.Length != 2 || !int.TryParse(args[1], out int port))
			{
				port = 8888;
			}
			var client = new Client(args[0], port);

			try
			{
				client.Run();
			}
			catch (AggregateException e)
			{
				Console.WriteLine("Network error occured: " + e.Message);
			}
			catch (Exception e)
			{
				Console.WriteLine("Error occured: " + e.Message);
			}

			Console.WriteLine("Finishing program");
		}
	}
}
