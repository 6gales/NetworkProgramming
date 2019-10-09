using System.Collections.Generic;
using System.Threading.Tasks;
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
			
			TreeNode node = createFromParams[args.Length].Invoke();
			node.Run();
		}
	}
}
