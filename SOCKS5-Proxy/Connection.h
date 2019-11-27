#pragma once

#include <unistd.h>
#include <sys/types.h>
#include <sys/socket.h>
#include <cstdio>

class Connection
{
protected:
	constexpr static size_t BUFF_SIZE = 4096;
	char sendBuffer[BUFF_SIZE],
		recvBuffer[BUFF_SIZE];

	const int connSockfd,
		coupledSockfd;

	bool eof;
	int sendOffset,
		recvOffset;

public:
	Connection(int connSock, int coupledSock) : connSockfd(connSock), coupledSockfd(coupledSock)
	{
		eof = false;
		sendOffset = 0;
		recvOffset = 0;
	}

	bool isActive()
	{
		return !eof || sendOffset == recvOffset;
	}

	void setFds(fd_set *readfds, fd_set *writefds)
	{
		if (!eof)
		{
			FD_SET(connSockfd, readfds);
		}
		FD_SET(coupledSockfd, writefds); 
	}

	int getFd()
	{
		return connSockfd;
	}

	virtual void proccessConnection(fd_set *readfds, fd_set *writefds);

	virtual ~Connection()
	{
		fprintf(stderr, "connection closing: %d\n", connSockfd);
		close(connSockfd);
	}
};