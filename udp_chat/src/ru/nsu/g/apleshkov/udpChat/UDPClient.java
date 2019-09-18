package ru.nsu.g.apleshkov.udpChat;

import java.io.IOException;
import java.net.DatagramPacket;
import java.net.DatagramSocket;
import java.net.InetAddress;
import java.net.SocketException;
import java.net.UnknownHostException;
import java.util.HashSet;
import java.util.Map;
import java.util.Set;

public class UDPClient
{
	private DatagramSocket ds;
	private InetAddress address;
	private int port;

	public UDPClient(String ipAddr, int port) throws UnknownHostException, SocketException
	{
		this.port = port;
		address = InetAddress.getByName(ipAddr);

		if (!address.isMulticastAddress())
		{
			System.out.println("Warning, this is not multicast address");
		}

		ds = new DatagramSocket();
	}

	void start()
	{
		String message = "Message from: " + address.getHostAddress() + ":" + port;
		byte[] data = message.getBytes();
		int timeout = 3000;

		for (int i = 0; i < 500; i++)
		{
			try
			{
				DatagramPacket packet = new DatagramPacket(data, data.length, address, port);
				ds.send(packet);
			}
			catch (IOException e)
			{
				e.printStackTrace();
			}
			Set<InetAddress> set = new HashSet<>();

			long start = System.currentTimeMillis(),
				end = start;

			try
			{
				ds.setSoTimeout(timeout);
			}
			catch (SocketException e)
			{
				e.printStackTrace();
			}

			while (end - start < timeout)
			{
				try
				{
					DatagramPacket packet = new DatagramPacket(new byte[1024], 1024);
					ds.receive(packet);
					end = System.currentTimeMillis();
					set.add(packet.getAddress());
					System.out.println(new String(packet.getData()));
					if (end - start < timeout)
						ds.setSoTimeout(timeout - (int)(end - start));
				}
				catch (IOException e)
				{
					e.printStackTrace();
				}
			}
		}
	}
}