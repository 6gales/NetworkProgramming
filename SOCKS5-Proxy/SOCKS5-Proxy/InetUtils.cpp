#include "InetUtils.h"
#include <stdexcept>
#include <string.h>

struct sockaddr_in getAddr(std::string host, int port)
{
	struct sockaddr_in sockAddr;

	if (!host.empty())
	{
		struct addrinfo* addr = NULL;
		struct addrinfo hints;
		fillHints(&hints);
		
		int errorCode = getaddrinfo(host.c_str(), NULL, &hints, &addr);
		if (errorCode != 0)
		{
			throw std::invalid_argument(std::string("getaddrinfo: ") + gai_strerror(errorCode));
		}
		
		sockAddr = getAddr(addr, port);

		freeaddrinfo(addr);
    }
	else
	{
		sockAddr = getAddr(NULL, port);
	}

	return sockAddr;
}

struct sockaddr_in getAddr(struct addrinfo *addr, int port)
{
	struct sockaddr_in sockAddr;
	memset(&sockAddr, 0, sizeof(sockAddr));

	if (addr != NULL)
	{
		memcpy(&sockAddr, addr->ai_addr, sizeof(struct sockaddr));
	}
	else
	{
		sockAddr.sin_addr.s_addr = htonl(INADDR_ANY);
	}

	sockAddr.sin_family = AF_INET;
	sockAddr.sin_port = htons(port);

	return sockAddr;
}

void fillHints(struct addrinfo* hints)
{
	memset(hints, 0, sizeof(addrinfo));
	hints->ai_family = AF_INET;
	hints->ai_socktype = SOCK_STREAM;
	hints->ai_protocol = IPPROTO_TCP;
}

int openRedirectedSocket(struct sockaddr_in sockAddr)
{
	int sock = socket(AF_INET, SOCK_STREAM, 0);
	if (sock == -1 || connect(sock, (struct sockaddr*) &sockAddr, sizeof(sockAddr)))
	{
		throw std::runtime_error("redirecting failed");
	}
	return sock;
}