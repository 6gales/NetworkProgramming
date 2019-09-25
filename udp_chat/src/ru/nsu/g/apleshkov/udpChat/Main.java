package ru.nsu.g.apleshkov.udpChat;

import java.io.IOException;

public class Main
{
	public static void main(String[] args)
	{
		UDPClient client;
		if (args.length == 0 || args.length == 1 && args[0].equals("--default"))
		{
			client = new UDPClient();
		}
		else if (args.length == 2)
		{
			client = new UDPClient(args[0], Integer.parseInt(args[1]));
		}
		else if (args.length == 3)
		{
			client = new UDPClient(args[0], Integer.parseInt(args[1]), Integer.parseInt(args[2]));
		}
		else
		{
			System.out.println("Usage: <ip address> <port>");
			return;
		}

		try
		{
			client.start();
		}
		catch (IOException e)
		{
			e.printStackTrace();
		}
	}
}
