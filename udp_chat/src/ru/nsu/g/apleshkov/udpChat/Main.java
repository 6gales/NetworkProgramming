package ru.nsu.g.apleshkov.udpChat;

import java.net.SocketException;
import java.net.UnknownHostException;

public class Main
{
	public static void main(String[] args) throws SocketException, UnknownHostException
	{
		UDPClient client;
		if (args.length == 1 && args[0].equals("--default"))
		{
			client = new UDPClient("224.0.147.117", 8080);
		}
		else if (args.length == 2)
		{
			client = new UDPClient(args[0], Integer.parseInt(args[1]));
		}
		else
		{
			System.out.println("Usage: <ip address> <port>");
			return;

		}

		client.start();

	}
}
