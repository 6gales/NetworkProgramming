using System.Collections.Generic;
using System;

namespace СhatTree
{
	class Program
	{
		delegate TreeNode treeNodeCreator();

		static void Main(string[] args)
		{
			Dictionary<int, treeNodeCreator> createFromParams = new Dictionary<int, treeNodeCreator>()
			{
				[3] = () => new TreeNode(args[0], int.Parse(args[1]), int.Parse(args[2])),
				[5] = () => new TreeNode(args[0], int.Parse(args[1]), int.Parse(args[2]), args[3], int.Parse(args[4]))
			};

			if (!createFromParams.ContainsKey(args.Length))
			{
				Console.WriteLine("Usage: <name> <loss rate> <port to bind> <parent node ip> <parent node port>");
				return;
			}

			try
			{
				TreeNode node = createFromParams[args.Length].Invoke();
				createFromParams.Clear();

				node.Run();
			}
			catch (Exception e)
			{
				Console.Error.WriteLine(e.Message + e.StackTrace);
			}
		}
	}
}
