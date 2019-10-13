using System;
using System.Net;

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
			_endPoint = endPoint;
		}

		public IPEndPoint GetIPEndPoint()
		{
			if (_endPoint == null)
				_endPoint = new IPEndPoint(IPAddress.Parse(_host), _port);
			return _endPoint;
		}
	}
}
