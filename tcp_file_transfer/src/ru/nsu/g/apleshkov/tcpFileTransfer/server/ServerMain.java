package ru.nsu.g.apleshkov.tcpFileTransfer.server;

public class ServerMain
{
	public static void main(String[] args)
	{
		Server server;
		if (args.length == 0)
		{
			server = new Server(8080);
		}
		else if (args.length == 1)
		{
			server = new Server(Integer.parseInt(args[0]));
		}
		else
		{
			System.out.println("Usage: <port>");
			return;
		}

		try
		{
			server.run();
		}
		catch (Exception e)
		{
			e.printStackTrace();
		}
	}
}
