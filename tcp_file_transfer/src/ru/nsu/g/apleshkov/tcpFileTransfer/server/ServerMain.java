package ru.nsu.g.apleshkov.tcpFileTransfer.server;

import java.util.HashMap;
import java.util.concurrent.Callable;

public class ServerMain
{
	public static void main(String[] args)
	{
		HashMap<Integer, Callable<Server>> createFromParams
				= new HashMap<Integer, Callable<Server>>() {{
					put(0, Server::new);
					put(1, () -> new Server(Integer.parseInt(args[0])));
					put(2, () -> new Server(Integer.parseInt(args[0]), Integer.parseInt(args[1])));
		}};

		if (!createFromParams.containsKey(args.length))
		{
			System.out.println("Usage: <timeout> <port>");
			return;
		}

		try
		{
			Server server = createFromParams.get(args.length).call();
			server.run();
		}
		catch (Exception e)
		{
			e.printStackTrace();
		}
	}
}
