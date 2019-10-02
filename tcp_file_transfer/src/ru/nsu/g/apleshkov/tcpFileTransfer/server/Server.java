package ru.nsu.g.apleshkov.tcpFileTransfer.server;

import java.io.File;
import java.net.InetAddress;
import java.net.ServerSocket;
import java.net.Socket;

public class Server
{
	private int port,
				timeout;
	private String uploadFolder = "uploads";

	public Server()
	{
		this(3000, 8080);
	}

	public Server(int timeout)
	{
		this(timeout, 8080);
	}

	public Server(int timeout, int port)
	{
		this.timeout = timeout;
		this.port = port;
	}

	public void run() throws Exception
	{
		File uploads = new File(uploadFolder);
		if (!uploads.exists())
			if (!uploads.mkdir())
				throw new Exception("Cannot create folder");

		try (ServerSocket serverSocket = new ServerSocket(port))
		{
			System.out.println("Listening " + InetAddress.getLocalHost().getHostAddress() + ":" + serverSocket.getLocalPort());
			while (true)
			{
				Socket socket = serverSocket.accept();
				System.out.println("New user connected: " + socket.getInetAddress());
				new UserThread(socket, uploadFolder + "/", timeout).start();
			}
		}
	}
}
