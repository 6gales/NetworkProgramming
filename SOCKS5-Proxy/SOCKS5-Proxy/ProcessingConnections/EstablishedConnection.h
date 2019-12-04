#pragma once
#include <unistd.h>
#include <sys/types.h>
#include <sys/socket.h>
#include <cstdio>
#include "../Socks5Context.h"

class ConnectionBuffer
{
public:
	constexpr static size_t BUFF_SIZE = 4096;

private:
	char buffer[BUFF_SIZE];
	size_t readOffset,
		writeOffset;

public:
	bool eof;

	ConnectionBuffer()
	{
		readOffset = 0;
		writeOffset = 0;
		eof = false;
	}

	bool isFull() { return writeOffset == BUFF_SIZE; }

	bool hasData() { return writeOffset != readOffset; }

	char* getData() { return buffer; }

	char* getUnread() { return buffer + readOffset; }
	size_t countUnread() { return writeOffset - readOffset; }

	char* getEmptyData() { return buffer + writeOffset; }
	size_t countEmpty() { return BUFF_SIZE - writeOffset; }

	void addRead(size_t bytesRead)
	{
		writeOffset += bytesRead;
	}

	void addWrote(size_t bytesWrote)
	{
		readOffset += bytesWrote;
	}

	bool isFinished()
	{
		return eof && readOffset == writeOffset;
	}

	void reset()
	{
		writeOffset = 0;
		readOffset = 0;
	}
};

class EstablishedConnection : public Socks5Context::ProcessingState
{
	ConnectionBuffer clientBuffer,
		servBuffer;

	const int clientSock,
		servSock;

public:
	EstablishedConnection(int client, int serv) : clientSock(client), servSock(serv) {}

	ProcessingState* process(Socks5Context* context) override;

	void exchangeData(Socks5Context* context, int fromFd, int toFd, ConnectionBuffer& buffer);

	~EstablishedConnection()
	{
		close(clientSock);
		close(servSock);
	}
};