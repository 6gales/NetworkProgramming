#pragma once
#include <netdb.h>
#include <cstring>
#include <unistd.h>
#include "../Socks5Context.h"

class DnsResolver : public Socks5Context::ProcessingState
{
	int sockfd,
		port;

	bool failed = false;

	struct gaicb* request;
	struct addrinfo hints;

public:

	DnsResolver(int _sockfd, std::string domain, int port);

	ProcessingState* process(Socks5Context* context) override;

	~DnsResolver()
	{
		free(request);
		if (failed)
		{
			close(sockfd);
		}
	}
};

