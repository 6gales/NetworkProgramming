package ru.nsu.g.apleshkov.tcpFileTransfer.server;

import ru.nsu.g.apleshkov.tcpFileTransfer.commonutils.ChecksumUtil;
import ru.nsu.g.apleshkov.tcpFileTransfer.commonutils.IOUtils;

import java.io.File;
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.net.Socket;
import java.net.SocketTimeoutException;

public class UserThread extends Thread
{
	private Socket socket;
	private String filepath;
	private int timeout;

	UserThread(Socket socket, String filepath, int timeout)
	{
		this.socket = socket;
		this.filepath = filepath;
		this.timeout = timeout;
	}

	private void closeSocket()
	{
		try
		{
			socket.close();
		}
		catch (IOException e)
		{
			e.printStackTrace();
		}
	}

	private File createFile(String fileName) throws Exception
	{
		String name,
			extension = "";

		int dotInd = fileName.lastIndexOf('.');
		if (dotInd > 0)
		{
			name = fileName.substring(0, dotInd);
			extension = fileName.substring(dotInd);
		}
		else name = fileName;

		File file = new File(filepath + fileName);
		for (int i = 1; file.exists(); i++)
		{
			file = new File(filepath + name + i + extension);
		}

		if (!file.createNewFile())
			throw new Exception("Cannot create file");

		return file;
	}

	private ChecksumUtil downloadFile(InputStream in, File file, long fileLen) throws IOException
	{
		ChecksumUtil checksumUtil = new ChecksumUtil();
		try (OutputStream out = new FileOutputStream(file))
		{

			long downloadStart = System.currentTimeMillis(),
					readLen = 0;

			int buffSize = 4096;
			byte[] buffer = new byte[buffSize];

			while (readLen < fileLen)
			{
				long partialLen = 0;
				try
				{
					long start = System.currentTimeMillis(),
							end = start;
					do
					{
						socket.setSoTimeout(timeout - (int)(end - start));

						int readBytes = (fileLen - readLen > buffSize
								? in.read(buffer)
								: in.read(buffer, 0, (int)(fileLen - readLen)));

						if (readBytes == 0)
							break;

						partialLen += readBytes;
						readLen += readBytes;

						checksumUtil.update(buffer, 0, readBytes);
						out.write(buffer, 0, readBytes);

						end = System.currentTimeMillis();

					} while (end - start < timeout && readLen < fileLen);

				} catch (SocketTimeoutException ignore) {}

				System.out.println(socket.getInetAddress()
						                   + ":\n\tCurrent speed: " + partialLen / (timeout / 1000.0) + " b/s"
						                   + "\n\tAverage speed: "
						                   + readLen / ((System.currentTimeMillis() - downloadStart) / 1000.0) + " b/s");
			}
		}
		return checksumUtil;
	}

	private <T extends Comparable<T>> void checkProtocol(T wanted, T got) throws IOException
	{
		if (wanted.compareTo(got) > 0)
			throw new IOException("protocol non-compliance detected");
	}

	@Override
	public void run()
	{
		try
		{
			InputStream in = socket.getInputStream();
			IOUtils io = new IOUtils(in, socket.getOutputStream());

			int nameLen = io.readInt();
			byte[] buffer = new byte[nameLen];

			checkProtocol(nameLen, in.read(buffer));

			File file = createFile(new String(buffer));

			long fileLen = io.readLong();
			ChecksumUtil checksumUtil = downloadFile(in, file, fileLen);

			int hashLen = io.readInt();
			buffer = new byte[hashLen];

			checkProtocol(hashLen, in.read(buffer));

			byte[] digest = checksumUtil.digest();
			boolean result = checksumUtil.isEqual(digest, buffer);

			System.out.println(socket.getInetAddress()
									+ "\n\tConstructed hash: \"" + checksumUtil.convertToString(digest) + "\""
									+ "\n\tReceived hash:    \"" + checksumUtil.convertToString(buffer) + "\""
									+ "\n\tEquals: " + result);

			byte[] response = (result ? "File received without errors" : "File's hash does not match").getBytes();
			io.writeInt(response.length);
			socket.getOutputStream().write(response);
		}
		catch (Exception e)
		{
			e.printStackTrace();
		}
		finally
		{
			closeSocket();
		}
	}
}