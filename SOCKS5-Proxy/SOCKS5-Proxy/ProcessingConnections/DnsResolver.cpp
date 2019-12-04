#include "DnsResolver.h"
#include "../InetUtils.h"
#include "EstablishedConnection.h"

DnsResolver::DnsResolver(int _sockfd, std::string domain, int _port) : sockfd(_sockfd), port(_port)
{
	request = (struct gaicb*)malloc(sizeof(struct gaicb*));
	request->ar_name = domain.c_str();
	request->ar_service = NULL;
	fillHints(&hints);
	request->ar_request = &hints;

	int errorCode = getaddrinfo_a(GAI_NOWAIT, &request, 1, NULL);
	if (errorCode)
	{
		throw std::runtime_error(std::string("getaddrinfo: ") + gai_strerror(errorCode));
	}
}

Socks5Context::ProcessingState* DnsResolver::process(Socks5Context* context)
{
	int status = gai_error(request);
	switch (status)
	{
	case 0:
	{
		int servfd = openRedirectedSocket(getAddr(request->ar_result, port));
		freeaddrinfo(request->ar_result);

		return new EstablishedConnection(sockfd, servfd);
	}
	case EAI_INPROGRESS:
		break;

	default:
		failed = true;
		context->finish();
	}
	return nullptr;
}