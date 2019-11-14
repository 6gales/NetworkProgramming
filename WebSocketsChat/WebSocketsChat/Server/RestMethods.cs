using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using WebSocketsChat.ModelDefinition;

namespace WebSocketsChat.Server
{
	class RestMethods
	{
		public delegate void PostResource<T>(T resource);
		public delegate bool GetResourceById<T>(int id, out T value);
		public delegate bool GetResourceByQuery<T>(Dictionary<string, string> query, out IEnumerable<T> resource);
		public delegate bool DeleteResource(int id);

		public delegate bool VerifyUser(HttpListenerRequest request);

		public RestMethods(VerifyUser verify)
		{
			_verify = verify;
		}

		private readonly VerifyUser _verify;

		public void PerformDelete(HttpListenerContext context, DeleteResource deleteResource, string parameter)
		{
			if (_verify != null && !_verify(context.Request))
			{
				WriteError(context.Response, HttpStatusCode.Unauthorized, "Authorization failed");
				return;
			}

			if (int.TryParse(parameter, out int id))
			{
				if (deleteResource.Invoke(id))
				{
					context.Response.StatusCode = (int)HttpStatusCode.NoContent;
					context.Response.OutputStream.Close();
				}
				else WriteError(context.Response, HttpStatusCode.NotFound,
								"resource with provided id not found");
			}
			else WriteError(context.Response, HttpStatusCode.BadRequest, parameter + " unexpected");
		}

		public static bool ParseQuery(string query, out Dictionary<string, string> queryDictionary)
		{
			queryDictionary = null;
			if (string.IsNullOrEmpty(query))
			{
				return true;
			}

			query = query.Substring(1);
			queryDictionary = new Dictionary<string, string>();

			var pairs = query.Split('&');
			foreach (var pair in pairs)
			{
				var splited = pair.Split('=');
				if (splited.Length != 2)
					return false;
					
				queryDictionary.Add(splited[0], splited[1]);
			}
			return true;
		}

		public void PerformGet<T>(HttpListenerContext context, GetResourceByQuery<T> getResource)
		{
			if (_verify != null && !_verify(context.Request))
			{
				WriteError(context.Response, HttpStatusCode.Unauthorized, "Authorization failed");
				return;
			}

			string query = context.Request.Url.Query;

			if (ParseQuery(query, out Dictionary<string, string> queryDictionary)
				&& getResource.Invoke(queryDictionary, out IEnumerable<T> resources))
			{
				WriteToResponse(context.Response, resources);
				return;
			}

			WriteError(context.Response, HttpStatusCode.BadRequest, "bad query parameters");
		}

		public void PerformGetById<T>(HttpListenerContext context, GetResourceById<T> getResource, string parameter)
		{
			if (_verify != null && !_verify(context.Request))
			{
				WriteError(context.Response, HttpStatusCode.Unauthorized, "Authorization failed");
				return;
			}

			if (int.TryParse(parameter, out int id))
			{
				if (getResource.Invoke(id, out T value))
				{
					WriteToResponse(context.Response, value);
				}
				else WriteError(context.Response, HttpStatusCode.NotFound,
								"resource with provided id not found");
			}
			else WriteError(context.Response, HttpStatusCode.BadRequest, parameter + " unexpected");
		}

		public void PerformPost<T>(HttpListenerContext context, PostResource<T> postResource)
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

			postResource(message);

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
			var responseStr = JsonConvert.SerializeObject(message);
			var buffer = Encoding.UTF8.GetBytes(responseStr);
			
			try
			{
				response.ContentLength64 = buffer.Length;
				response.ContentType = "application/json";

				using (var output = response.OutputStream)
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