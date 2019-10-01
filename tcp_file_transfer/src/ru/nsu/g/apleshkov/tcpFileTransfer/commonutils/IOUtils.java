package ru.nsu.g.apleshkov.tcpFileTransfer.commonutils;

import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.nio.ByteBuffer;

public class IOUtils
{
	private static final int IntSize = 4,
							LongSize = 8;

	private InputStream in;
	private OutputStream out;

	public IOUtils(InputStream in, OutputStream out)
	{
		this.in = in;
		this.out = out;
	}

	public void writeInt(int data) throws IOException
	{
		out.write(ByteBuffer.allocate(IntSize).putInt(data).array());
	}

	public void writeLong(long data) throws IOException
	{
		out.write(ByteBuffer.allocate(LongSize).putLong(data).array());
	}

	public int readInt() throws IOException
	{
		byte[] buffer = new byte[IntSize];
		if (in.read(buffer) < IntSize)
			throw new IOException("Not enough bytes to build int");
		return ByteBuffer.wrap(buffer).getInt();
	}

	public long readLong() throws IOException
	{
		byte[] buffer = new byte[LongSize];
		if (in.read(buffer) < LongSize)
			throw new IOException("Not enough bytes to build long");
		return ByteBuffer.wrap(buffer).getLong();
	}
}
