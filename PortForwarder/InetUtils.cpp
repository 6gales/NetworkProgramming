#include "InetUtils.h"
#include <stdexcept>
#include <string.h>

struct sockaddr_in getAddr(std::string host, int port)
{
	struct sockaddr_in sockAddr;
	struct addrinfo *addr = NULL;

	memset(&sockAddr, 0, sizeof(sockAddr));

	if (!host.empty())
	{
		struct addrinfo hints;
		memset(&hints, 0, sizeof(addrinfo));
		hints.ai_family = AF_INET;
		hints.ai_socktype = SOCK_STREAM;
		hints.ai_protocol = IPPROTO_TCP;
		
		int errorCode = getaddrinfo(host.c_str(), NULL, &hints, &addr);
		if (errorCode != 0)
		{
			throw std::invalid_argument(std::string("getaddrinfo: ") + gai_strerror(errorCode));
		}
		memcpy(&sockAddr, addr->ai_addr, sizeof(struct sockaddr));
		freeaddrinfo(addr);
    }
	else
	{
		sockAddr.sin_addr.s_addr = htonl(INADDR_ANY);
	}
	
	sockAddr.sin_family = AF_INET;
	sockAddr.sin_port = htons(port);

	return sockAddr;
}