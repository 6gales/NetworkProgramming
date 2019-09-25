package ru.nsu.g.apleshkov.udpChat;

import java.io.IOException;
import java.net.DatagramPacket;
import java.net.InetAddress;
import java.net.MulticastSocket;
import java.net.SocketTimeoutException;
import java.util.HashMap;
import java.util.LinkedList;
import java.util.List;
import java.util.Map;

public class UDPClient
{
	private String hostname;
	private int port;
	private int timeout;

	public UDPClient()
	{
		this("224.0.147.0", 8888, 3000);
	}

	public UDPClient(String ipAddr, int port)
	{
		this(ipAddr, port, 3000);
	}

	public UDPClient(String ipAddr, int port, int timeout)
	{
		this.port = port;
		hostname = ipAddr;
		this.timeout = timeout;
	}

	void start() throws IOException
	{
		InetAddress address = InetAddress.getByName(hostname);

		String message = "Message from " + InetAddress.getLocalHost();
		byte[] data = message.getBytes();

		int buffLen = 128,
			iterationsToLive = 4;

		byte[] receiveBuff = new byte[buffLen];
		Map<InetAddress, Integer> knownCopies = new HashMap<>();
		List<InetAddress> keysForDelete = new LinkedList<>();

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

						DatagramPacket packet = new DatagramPacket(receiveBuff, buffLen);
						socket.receive(packet);
						knownCopies.put(packet.getAddress(), 0);
						System.out.println("Received \"" + new String(packet.getData()).trim() + "\" from " + packet.getAddress().toString());
					}
					catch (SocketTimeoutException ignore) {}

				} while (System.currentTimeMillis() - start < timeout);

				System.out.println(knownCopies.size() + " copies detected");
				knownCopies.forEach((InetAddress addr, Integer i) ->
				{
					if (++i > iterationsToLive)
						keysForDelete.add(addr);
					else
					{
						System.out.println("Copy: " + addr + ", Last seen " + i + " iterations ago");
						knownCopies.put(addr, i);
					}
				});

				keysForDelete.forEach(knownCopies::remove);
				keysForDelete.clear();
			}
		}
	}
}