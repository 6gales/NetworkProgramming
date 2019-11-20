#include "Connection.h"

void Connection::proccessConnection(fd_set *readfds, fd_set *writefds)
{
	if (FD_ISSET(connSockfd, readfds) && BUFF_SIZE != recvOffset)
	{
		int bytesRead = recv(connSockfd, recvBuffer + recvOffset, BUFF_SIZE - recvOffset, 0);
		if (0 == bytesRead)
		{
			eof = true;
		}
		recvOffset += bytesRead;
	}
	if (FD_ISSET(coupledSockfd, writefds) && sendOffset != recvOffset)
	{
		int bytesWrote = send(coupledSockfd, recvBuffer + sendOffset, recvOffset - sendOffset, 0);
		sendOffset += bytesWrote;
		if (BUFF_SIZE == recvOffset && BUFF_SIZE == sendOffset)
		{
			recvOffset = 0;
			sendOffset = 0;
		}
	}
}