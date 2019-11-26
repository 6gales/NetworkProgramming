#include "Socks5Connection.h"

void Socks5Connection::proccessConnection(fd_set *readfds, fd_set *writefds)
{
	if (FD_ISSET(clientFd, readfds) && !eof)
	{
		int bytesRead = recv(clientFd, recvBuffer + recvOffset, BUFF_SIZE - recvOffset, 0);
		if (0 == bytesRead)
		{
			eof = true;
		}
		recvOffset += bytesRead;
	}

	if (!proccessData())
	{
		eof = true;
		initialStage = false;
		succseed = false;
	}

	if (FD_ISSET(clientFd, writefds) && needWrite)
	{
		int bytesWrote = send(clientFd, recvBuffer + sendOffset, sendSize, 0);
		if (-1 == bytesWrote)
		{
			return;
		}

		sendOffset += bytesWrote;
		if (sendOffset == sendSize)
		{
			needWrite = false;
			sendOffset = 0;
			sendSize = 0;
		}
	}
}

bool Socks5Connection::proccessData()
{
	//min for first and second negotiation stage
	if (recvOffset < (initialStage ? 2 : 10))
	{
		return !eof;
	}

	if (recvBuffer[0] != socksVersion)
	{
		return false;
	}

	sendBuffer[0] = socksVersion;

	if (initialStage)
	{
		return performFirstStage();
	}

	sendBuffer[1] = (connectCommand != recvBuffer[1] ? commandNotSupported : accepted);
	sendBuffer[2] = reserved;

	switch (recvBuffer[3])
	{
		case ipv4:
			domain = false;
			addr += std::to_string(recvBuffer[4]) + '.' + std::to_string(recvBuffer[5]) + '.' +
					std::to_string(recvBuffer[6]) + '.' + std::to_string(recvBuffer[7]);
			((unsigned char *) &port)[1] = recvBuffer[8];
			((unsigned char *) &port)[0] = recvBuffer[9];
			break;
		
		case domainName:
		{
			domain = true;
			char len = recvBuffer[4];
			if (recvOffset < len + 7)
			{
				return true;
			}
		
			for (char i = 0; i < len; i++)
			{
				addr.push_back(recvBuffer[i + 5]);
			}

			((unsigned char *) &port)[1] = recvBuffer[len + 5];
			((unsigned char *) &port)[0] = recvBuffer[len + 6];
			break;
		}
		default:
			sendBuffer[1] = addressNotSupported;
	}
	
	sendBuffer[3] = ipv4;
	//ip
	sendBuffer[4] = 127;
	sendBuffer[5] = 0;
	sendBuffer[6] = 0;
	sendBuffer[7] = 1;

	//port
	sendBuffer[8] = 0;//((unsigned char *) &port)[0];
	sendBuffer[9] = 0;//((unsigned char *) &port)[1];

	needWrite = true;
	return true;
}

bool Socks5Connection::performFirstStage()
{
	if (recvBuffer[0] != socksVersion)
	{
		return false;
	}

	char authMethods = recvBuffer[1];
	if (authMethods + 2 > recvOffset)
	{
		return true;
	}

	sendBuffer[1] = noAcceptableMethods;
	bool success = false;
	sendSize = 2;

	for (char i = 2; i < authMethods + 2; i++)
	{
		if (noAuthentication == recvBuffer[i])
		{
			sendBuffer[1] = noAuthentication;
			success = true;
		}
	}

	needWrite = true;
	initialStage = false;
	return success;
}