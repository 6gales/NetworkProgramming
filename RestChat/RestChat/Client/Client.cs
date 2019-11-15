using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using RestChat.ModelDefinition;
using System.Net.Http.Headers;
using System.Threading;

namespace RestChat.Client
{
	class Client
	{
		private bool _notExited = true;
		private readonly string _httpAddress;

		private delegate void Command(string parameters);
		private readonly Dictionary<string, Command> _consoleCommands;

		private List<Message> _messages;
		private IEnumerable<User> _users;

		private HttpClient _httpClient;

		Client(string server, int port)
		{
			_httpAddress = "http://" + server + ":" + port + "/";

			_consoleCommands = new Dictionary<string, Command>()
			{
				["/help"] = (s)
					=> Console.WriteLine("Commands: " + string.Join("\n\t", _consoleCommands.Keys)),
				["/list"] = (s)
					=> Console.WriteLine("User list:\n\t" + string.Join(",\n\t", _users)),
				["/user"] = GetUser,
				["/delete"] = DeleteMessage,
				["/logout"] = (s) => _notExited = false,
				["/messages"] = (s) => ShowMessages()
			};
		}

		private static T GetFromResponse<T>(HttpResponseMessage response) 
			=> JsonConvert.DeserializeObject<T>(response.Content.ReadAsStringAsync().Result);

		private void GetUser(string param)
		{
			var parsed = param.Split(' ');
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

		private void ShowMessages()
		{
			foreach (var message in _messages)
			{
				Console.WriteLine(message);
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

		protected async Task<string> GetLineAsync() => await Task.Run(() => Console.ReadLine());

		private Task<HttpResponseMessage> PostTo<T>(string url, T content)
		{
			HttpContent httpContent = new StringContent(JsonConvert.SerializeObject(content), Encoding.UTF8, "application/json");
			return _httpClient.PostAsync(_httpAddress + url, httpContent);
		}

		void Run()
		{
			using (HttpClient httpClient = new HttpClient())
			{
				httpClient.Timeout = Timeout.InfiniteTimeSpan;
				_httpClient = httpClient;
				HttpResponseMessage resp = null;
				Console.Write("Enter your nickname: ");
				do
				{
					if (resp != null)
						Console.Write("Sorry, user with this nickname is already exists. \nPlease, choose another one: ");
					LoginRequest loginRequest = new LoginRequest { Username = Console.ReadLine() };
					resp = PostTo("/login", loginRequest).Result;

				} while (resp.StatusCode != System.Net.HttpStatusCode.OK);

				LoginResponse login = JsonConvert.DeserializeObject<LoginResponse>(resp.Content.ReadAsStringAsync().Result);
				_users = new User[] { login };

				httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);

				resp = httpClient.GetAsync(_httpAddress + "/messages?count=30&end=true").Result;
				_messages = JsonConvert.DeserializeObject<List<Message>>(resp.Content.ReadAsStringAsync().Result);
				ShowMessages();

				Task<string> getLine = GetLineAsync();
				Task<HttpResponseMessage> messageRequest = httpClient.GetAsync(_httpAddress + "/messages");
				Task<HttpResponseMessage> userRequest = httpClient.GetAsync(_httpAddress + "/users");

				while (_notExited)
				{
					if (getLine.IsCompleted)
					{
						var line = getLine.Result;
						if (!string.IsNullOrEmpty(line))
						{
							var pos = line.IndexOf(" ", StringComparison.Ordinal);
							var command = (pos > 0 ? line.Substring(0, pos) : line);

							if (_consoleCommands.TryGetValue(command, out var cmd))
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

					if (messageRequest.IsCompleted)
					{
						var messages = JsonConvert.DeserializeObject<IEnumerable<Message>>(messageRequest.Result.Content.ReadAsStringAsync().Result);
						foreach (var message in messages)
						{
							Console.WriteLine(message);
							_messages.Add(message);
						}

						messageRequest = httpClient.GetAsync(_httpAddress + "/messages");
					}

					if (userRequest.IsCompleted)
					{
						_users = JsonConvert.DeserializeObject<IEnumerable<User>>(userRequest.Result.Content.ReadAsStringAsync().Result);
						Console.WriteLine("Users:");
						foreach (var user in _users)
						{
							Console.WriteLine("\t" + user);
						}

						userRequest = httpClient.GetAsync(_httpAddress + "/users");
					}

					Thread.Sleep(500);
				}

				httpClient.PostAsync(_httpAddress + "/logout", null).Wait();
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
