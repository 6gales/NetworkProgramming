﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RestChat.Server
{
	delegate void HandlerFunc(HttpListenerContext context);

	class Router
	{
		public static string IntegerUrlParameter { get; } = @"^[0-9]+$";
		private Dictionary<string, Regex> _parameterMapping;

		public PathTreeNode PathTree { get; private set; }

		public Router()
		{
			_parameterMapping = new Dictionary<string, Regex>
			{
				[IntegerUrlParameter] = new Regex(IntegerUrlParameter)
			};
			
			PathTree = new PathTreeNode();
		}

		public void AddHandler(string url, HttpMethod method, HandlerFunc handler)
		{
			if (string.IsNullOrEmpty(url) || (url.Length == 1 && url[0] == '/'))
			{
				PathTree.Add(method, handler);
				return;
			}
			
			if (url[0] == '/')
			{
				url = url.Substring(1);
			}

			string[] path = url.Split('/');

			PathTreeNode finiteNode = PathTree;
			foreach (var segment in path)
			{
				if (finiteNode.TryGetSubNode(segment, out PathTreeNode sub))
				{
					if (sub != null)
						finiteNode = sub;
				}
				else
				{
					finiteNode = finiteNode.Add(segment);
				}
			}
			finiteNode.Add(method, handler);
		}

		public bool TryGetHandler(HttpListenerRequest request, out HandlerFunc handler)
		{
			string[] segments = request.Url.AbsolutePath.Split('/');
			var finiteNode = PathTree;
			for (int i = 1; i < segments.Length; i++)
			{
				if (finiteNode.TryGetSubNode(segments[i], out PathTreeNode sub))
				{
					finiteNode = sub;
				}
				else
				{
					foreach (var pattern in _parameterMapping)
					{
						if (pattern.Value.IsMatch(segments[i]))
						{
							segments[i] = pattern.Key;
							break;
						}
					}
					if (finiteNode.TryGetSubNode(segments[i], out sub))
					{
						finiteNode = sub;
					}
				}
			}

			return finiteNode.TryGetHandler(request.HttpMethod, out handler);
		}
	}

	class PathTreeNode
	{
		private Dictionary<HttpMethod, HandlerFunc> _handlerFuncs;
		private Dictionary<string, PathTreeNode> _subDomains;

		public PathTreeNode()
		{
			_handlerFuncs = new Dictionary<HttpMethod, HandlerFunc>();
			_subDomains = new Dictionary<string, PathTreeNode>();
		}

		public void Add(HttpMethod method, HandlerFunc handler)
		{
			_handlerFuncs.Add(method, handler);
		}

		public PathTreeNode Add(string subDomain)
		{
			if (string.IsNullOrEmpty(subDomain))
				return null;

			var sub = new PathTreeNode();
			int pos = subDomain.IndexOf('/');
			if (pos > 0)
			{
				subDomain = subDomain.Substring(0, pos);
			}

			_subDomains.Add(subDomain, sub);
			return sub;
		}

		public bool TryGetSubNode(string subDomain, out PathTreeNode treeNode)
		{
			return _subDomains.TryGetValue(subDomain, out treeNode);
		}

		public bool TryGetHandler(string method, out HandlerFunc handler)
		{
			var httpMethod = new HttpMethod(method);
			return _handlerFuncs.TryGetValue(httpMethod, out handler);
		}
	}
}
