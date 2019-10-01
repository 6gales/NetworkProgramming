package ru.nsu.g.apleshkov.tcpFileTransfer.commonutils;

import java.security.MessageDigest;
import java.security.NoSuchAlgorithmException;

public class ChecksumUtil
{
	private MessageDigest md;

	public ChecksumUtil()
	{
		try
		{
			md = MessageDigest.getInstance("MD5");
		}
		catch (NoSuchAlgorithmException impossible)
		{
			impossible.printStackTrace();
		}
	}

	public void update(byte[] buffer, int off, int bytes)
	{
		md.update(buffer, off, bytes);
	}

	public String convertToString(byte[] bytes)
	{
		StringBuilder sb = new StringBuilder();
		for (byte aByte : bytes)
		{
			sb.append(Integer.toString((aByte & 0xff) + 0x100, 16).substring(1));
		}
		return sb.toString();
	}

	public byte[] digest()
	{
		return md.digest();
	}

	public boolean isEqual(byte[] one, byte[] another)
	{
		return MessageDigest.isEqual(one, another);
	}
}
