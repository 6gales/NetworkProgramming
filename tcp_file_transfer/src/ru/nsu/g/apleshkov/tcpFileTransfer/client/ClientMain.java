package ru.nsu.g.apleshkov.tcpFileTransfer.client;

import java.io.IOException;

public class ClientMain
{
	public static void main(String[] args)
	{
		Client client;
		if (args.length == 2)
		{
			client = new Client(args[0], args[1], 8080);
		}
		else if (args.length == 3)
		{
			client = new Client(args[0], args[1], Integer.parseInt(args[2]));
		}
		else
		{
			System.out.println("Usage: <filename> <host address> <port>");
			return;
		}

		try
		{
			client.run();
		}
		catch (IOException e)
		{
			e.printStackTrace();
		}
	}
}