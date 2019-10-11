using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ChatTree.MessageUtils
{
	[Serializable]
	class IPEndPointWrapper
	{
		private string _host;
		private int _port;

		[NonSerialized]
		private IPEndPoint _endPoint = null;

		public IPEndPointWrapper(IPEndPoint endPoint)
		{
			_host = endPoint.Address.ToString();
			_port = endPoint.Port;
		}

		public IPEndPointWrapper(string host, int port)
		{
			_host = host;
			_port = port;
		}

		public IPEndPoint GetIPEndPoint()
		{
			if (_endPoint == null)
				_endPoint = new IPEndPoint(IPAddress.Parse(_host), _port);
			return _endPoint;
		}
	}
}
