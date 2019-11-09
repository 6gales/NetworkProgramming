using Newtonsoft.Json;
using RestChat.ModelDefinition;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RestChat.Server
{
	class RestMethods
	{
		public delegate void PostResourse<T>(T resourse);
		public delegate bool GetResourseById<T>(int id, out T value);
		public delegate bool GetResourseByQuery<T>(Dictionary<string, string> query, out IEnumerable<T> resourse);
		public delegate bool DeleteResourse(int id);

		public delegate bool VerifyUser(HttpListenerRequest request);

		public RestMethods(VerifyUser verify)
		{
			_verify = verify;
		}

		private VerifyUser _verify;

		public void PerformDelete(HttpListenerContext context, DeleteResourse deleteResourse, string parameter)
		{
			if (_verify != null && !_verify(context.Request))
			{
				WriteError(context.Response, HttpStatusCode.Unauthorized, "Authorization failed");
				return;
			}

			if (int.TryParse(parameter, out int id))
			{
				if (deleteResourse.Invoke(id))
				{
					context.Response.StatusCode = (int)HttpStatusCode.NoContent;
					context.Response.OutputStream.Close();
				}
				else WriteError(context.Response, HttpStatusCode.NotFound,
								"resourse with provided id not found");
			}
			else WriteError(context.Response, HttpStatusCode.BadRequest, parameter + " unexpected");
		}

		public void PerformGet<T>(HttpListenerContext context, GetResourseByQuery<T> getResourse)
		{
			if (_verify != null && !_verify(context.Request))
			{
				WriteError(context.Response, HttpStatusCode.Unauthorized, "Authorization failed");
				return;
			}

			string query = context.Request.Url.Query;
			Dictionary<string, string> queryDictionary = null;
			if (!string.IsNullOrEmpty(query))
			{
				query = query.Substring(1);
				queryDictionary = new Dictionary<string, string>();

				string[] pairs = query.Split('&');
				foreach (var pair in pairs)
				{
					var splited = pair.Split('=');
					if (splited.Length != 2)
					{
						WriteError(context.Response, HttpStatusCode.BadRequest, "bad query parameters");
						return;
					}
					queryDictionary.Add(splited[0], splited[1]);
				}
			}

			if (getResourse.Invoke(queryDictionary, out IEnumerable<T> resourses))
			{
				WriteToResponse(context.Response, resourses);
				return;
			}

			WriteError(context.Response, HttpStatusCode.BadRequest, "bad query parameters");
		}

		public void PerformGetById<T>(HttpListenerContext context, GetResourseById<T> getResourse, string parameter)
		{
			if (_verify != null && !_verify(context.Request))
			{
				WriteError(context.Response, HttpStatusCode.Unauthorized, "Authorization failed");
				return;
			}

			if (int.TryParse(parameter, out int id))
			{
				if (getResourse.Invoke(id, out T value))
				{
					WriteToResponse(context.Response, value);
				}
				else WriteError(context.Response, HttpStatusCode.NotFound,
								"resourse with provided id not found");
			}
			else WriteError(context.Response, HttpStatusCode.BadRequest, parameter + " unexpected");
		}

		public void PerformPost<T>(HttpListenerContext context, PostResourse<T> postResourse)
		{
			if (_verify != null && !_verify(context.Request))
			{
				WriteError(context.Response, HttpStatusCode.Unauthorized, "Authorization failed");
				return;
			}

			T message;
			try
			{
				message = ReadBody<T>(context.Request);
			}
			catch (ArgumentException e)
			{
				WriteError(context.Response, HttpStatusCode.BadRequest, e.Message);
				return;
			}

			postResourse(message);

			context.Response.StatusCode = (int)HttpStatusCode.NoContent;
			context.Response.OutputStream.Close();
		}

		public static T ReadBody<T>(HttpListenerRequest request)
		{
			var body = new StreamReader(request.InputStream).ReadToEnd();
			if (string.IsNullOrEmpty(body))
			{
				throw new ArgumentException("body expected");
			}

			return JsonConvert.DeserializeObject<T>(body);
		}

		public static void WriteError(HttpListenerResponse response, HttpStatusCode code, string message)
		{
			response.StatusCode = (int)code;
			WriteToResponse(response, new Error { Message = message });
		}

		public static void WriteToResponse<T>(HttpListenerResponse response, T message)
		{
			string responseStr = JsonConvert.SerializeObject(message);
			byte[] buffer = Encoding.UTF8.GetBytes(responseStr);
			
			try
			{
				response.ContentLength64 = buffer.Length;
				response.ContentType = "application/json";

				using (Stream output = response.OutputStream)
				{
					output.Write(buffer, 0, buffer.Length);
				}
			}
			catch (HttpListenerException e)
			{
				Console.Error.WriteLine(e.Message);
			}
		}

		public static string GetToken(HttpListenerRequest request)
		{
			var values = request.Headers.Get(HttpRequestHeader.Authorization.ToString()).Split(' ');
			return values.Length == 2 ? values[1] : "";
		}
	}
}