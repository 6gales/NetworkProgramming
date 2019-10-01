package ru.nsu.g.apleshkov.tcpFileTransfer.server;

import java.io.File;
import java.net.ServerSocket;
import java.net.Socket;

public class Server
{
	private int port;
	private String uploadFolder = "uploads";

	public Server(int port)
	{
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
			while (true)
			{
				Socket socket = serverSocket.accept();
				System.out.println("New user connected: " + socket.getInetAddress());
				new UserThread(socket, uploadFolder + "/", 3000).start();
			}
		}
	}
}
