package ru.nsu.g.apleshkov.udpChat;

import java.io.IOException;
import java.net.DatagramPacket;
import java.net.InetAddress;
import java.net.MulticastSocket;
import java.net.SocketTimeoutException;
import java.net.UnknownHostException;
import java.util.HashSet;
import java.util.LinkedList;
import java.util.Set;

public class UDPClient
{
	private InetAddress address;
	private int port;
	private int timeout;

	public UDPClient() throws UnknownHostException
	{
		setSettings("224.0.147.117", 8888, 3000);
	}

	public UDPClient(String ipAddr, int port) throws UnknownHostException
	{
		setSettings(ipAddr, port, 3000);
	}

	public UDPClient(String ipAddr, int port, int timeout) throws UnknownHostException
	{
		setSettings(ipAddr, port, timeout);
	}

	private void setSettings(String ipAddr, int port, int timeout) throws UnknownHostException
	{
		this.port = port;
		address = InetAddress.getByName(ipAddr);
		this.timeout = timeout;

		if (!address.isMulticastAddress())
		{
			System.out.println("Warning, this is not multicast address");
		}
	}

	void start() throws IOException
	{
		String message = "Message from " + InetAddress.getLocalHost();
		byte[] data = message.getBytes();
//		Set<InetAddress> knownCopies = new HashSet<>();
		LinkedList<InetAddress> knownCopies = new LinkedList<>();

		try (MulticastSocket socket = new MulticastSocket(port))
		{
			socket.joinGroup(address);

			while (true)
			{
				socket.send(new DatagramPacket(data, data.length, address, port));

				long start = System.currentTimeMillis(),
						end = start;

				do
				{
					try
					{
						socket.setSoTimeout(timeout - (int)(end - start));

						DatagramPacket packet = new DatagramPacket(new byte[32], 32);
						socket.receive(packet);
						end = System.currentTimeMillis();
						knownCopies.add(packet.getAddress());
						System.out.println("Received \"" + new String(packet.getData()) + "\" from " + packet.getAddress().toString());
					}
					catch (SocketTimeoutException ignore) {}

					System.out.println("Copies found:");
					knownCopies.forEach(ia -> System.out.println("|_" + ia));
					knownCopies.clear();

				} while (end - start < timeout);
			}
		}
	}
}