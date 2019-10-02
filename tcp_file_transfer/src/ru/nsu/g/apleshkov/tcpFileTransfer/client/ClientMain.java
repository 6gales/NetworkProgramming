package ru.nsu.g.apleshkov.tcpFileTransfer.client;

import java.util.HashMap;
import java.util.concurrent.Callable;

public class ClientMain
{
	public static void main(String[] args)
	{
		HashMap<Integer, Callable<Client>> createFromParams
				= new HashMap<Integer, Callable<Client>>() {{
			put(2, () -> new Client(args[0], args[1], 8080));
			put(3, () -> new Client(args[0], args[1], Integer.parseInt(args[2])));
		}};

		if (!createFromParams.containsKey(args.length))
		{
			System.out.println("Usage: <filename> <host address> <port>");
			return;
		}

		try
		{
			Client client = createFromParams.get(args.length).call();
			client.run();
		}
		catch (Exception e)
		{
			e.printStackTrace();
		}
	}
}