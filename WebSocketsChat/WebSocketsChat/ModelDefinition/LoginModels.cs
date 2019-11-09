using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RestChat.ModelDefinition
{
	class LoginRequest
	{
		[JsonProperty("username")]
		public string Username { get; set; }
	}

	class LoginResponse : User
	{
		[JsonProperty("token")]
		public string Token { get; set; }
	}
}
