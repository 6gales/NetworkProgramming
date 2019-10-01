package ru.nsu.g.apleshkov.tcpFileTransfer.client;

import ru.nsu.g.apleshkov.tcpFileTransfer.commonutils.ChecksumUtil;
import ru.nsu.g.apleshkov.tcpFileTransfer.commonutils.IOUtils;

import java.io.File;
import java.io.FileInputStream;
import java.io.FileNotFoundException;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.net.Socket;

public class Client
{
	private String hostname;
	private int port;
	private File file;

	public Client(String filename, String hostname, int port)
	{
		this.hostname = hostname;
		this.port = port;

		file = new File(filename);
	}

	public void run() throws IOException
	{
		if (!file.exists())
			throw new FileNotFoundException(file.getName());

		ChecksumUtil checksumUtil = new ChecksumUtil();

		try (Socket socket = new Socket(hostname, port);
		     InputStream in = new FileInputStream(file))
		{
			OutputStream out = socket.getOutputStream();

			IOUtils io = new IOUtils(socket.getInputStream(), out);

			io.writeInt(file.getName().getBytes().length);

			out.write(file.getName().getBytes());

			io.writeLong(file.length());

			int readBytes;
			byte[] buffer = new byte[8192];
			while ((readBytes = in.read(buffer)) > 0)
			{
				checksumUtil.update(buffer, 0, readBytes);
				out.write(buffer, 0, readBytes);
			}

			byte[] digest = checksumUtil.digest();
			io.writeInt(digest.length);

			out.write(digest);

			int respLen = io.readInt();
			buffer = new byte[respLen];
			socket.getInputStream().read(buffer);
			System.out.println(new String(buffer));
		}
	}
}
