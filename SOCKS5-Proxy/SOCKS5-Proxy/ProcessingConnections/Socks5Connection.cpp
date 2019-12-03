#include "Socks5Connection.h"
#include <netinet/in.h>
#include "DnsResolver.h"
#include "EstablishedConnection.h"
#include "../InetUtils.h"

Socks5Context::ProcessingState* Socks5Connection::process(Socks5Context *context)
{
	if (context->getRegistrar().isSetRead(clientFd) && !eof)
	{
		ssize_t bytesRead = recv(clientFd, recvBuffer + recvOffset, BUFF_SIZE - recvOffset, 0);
		if (bytesRead < 0)
		{
			throw std::runtime_error(std::string("recv: ") + strerror(errno));
		}
		if (0 == bytesRead)
		{
			eof = true;
		}
		recvOffset += bytesRead;
	}

	if (!negotiate())
	{
		eof = true;
		initialStage = false;
		succeed = false;
		context->finish();
		return nullptr;
	}

	if (context->getRegistrar().isSetWrite(clientFd) && needWrite)
	{
		ssize_t bytesWrote = send(clientFd, sendBuffer + sendOffset, sendSize, 0);
		if (-1 == bytesWrote)
		{
			throw std::runtime_error(std::string("send: ") + strerror(errno));
		}

		sendOffset += bytesWrote;
		if (sendOffset == sendSize)
		{
			needWrite = false;
			sendOffset = 0;
			sendSize = 0;
		}
	}

	if (succeed)
	{
		if (domain)
		{
			return new DnsResolver(clientFd, addr, port);
		}

		int servFd = openRedirectedSocket(getAddr(addr, port));

		context->getRegistrar().registerFd(clientFd);
		context->getRegistrar().registerFd(servFd);

		return new EstablishedConnection(clientFd, servFd);
	}
	
	context->getRegistrar().registerFd(clientFd);

	return nullptr;
}

bool Socks5Connection::negotiate()
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
	if (connectCommand != recvBuffer[1])
		fprintf(stderr, "Command not supported\n");


	sendBuffer[2] = reserved;

	switch (recvBuffer[3])
	{
		case ipv4:
			domain = false;
			addr += std::to_string(recvBuffer[4]) + '.' + std::to_string(recvBuffer[5]) + '.' +
					std::to_string(recvBuffer[6]) + '.' + std::to_string(recvBuffer[7]);
			reinterpret_cast<unsigned char *>(&port)[1] = recvBuffer[8];
			reinterpret_cast<unsigned char *>(&port)[0] = recvBuffer[9];
			fprintf(stderr, "IPv4 here: %s:%d\n", addr.c_str(), port);
			break;
		
		case domainName:
		{
			domain = true;
			const unsigned char len = recvBuffer[4];
			if (recvOffset < len + 7)
			{
				return true;
			}
		
			for (char i = 0; i < len; i++)
			{
				addr.push_back(recvBuffer[i + 5]);
			}

			reinterpret_cast<unsigned char *>(&port)[1] = recvBuffer[len + 5];
			reinterpret_cast<unsigned char *>(&port)[0] = recvBuffer[len + 6];
			fprintf(stderr, "Domain here: %s:%d\n", addr.c_str(), port);
			break;
		}
		default:
			fprintf(stderr, "Address not supported\n");
			sendBuffer[1] = addressNotSupported;
	}
	
	sendBuffer[3] = ipv4;
	//ip
	sendBuffer[4] = 127;
	sendBuffer[5] = 0;
	sendBuffer[6] = 0;
	sendBuffer[7] = 1;

	//port
	sendBuffer[8] = 0;//((unsigned char *) &rPort)[0];
	sendBuffer[9] = 0;//((unsigned char *) &rPort)[1];

	succeed = true;
	sendSize = 10;
	needWrite = true;
	return true;
}

bool Socks5Connection::performFirstStage()
{
	if (recvBuffer[0] != socksVersion)
	{
		return false;
	}

	const unsigned char authMethods = recvBuffer[1];
	if (authMethods + 2 > recvOffset)
	{
		return true;
	}

	sendBuffer[1] = noAcceptableMethods;
	bool success = false;
	sendSize = 2;

	for (unsigned char i = 2; i < authMethods + 2; i++)
	{
		if (noAuthentication == recvBuffer[i])
		{
			sendBuffer[1] = noAuthentication;
			success = true;
		}
	}

	if (noAcceptableMethods == sendBuffer[1])
		fprintf(stderr, "No acceptable authorization methods\n");


	recvOffset = 0;
	needWrite = true;
	initialStage = false;
	return success;
}