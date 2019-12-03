#include "Socks5Context.h"
#include "ProcessingConnections/Socks5Connection.h"

Socks5Context::Socks5Context(int sockfd, FdRegistrar& _registrar) : registrar(_registrar)
{
	state = new Socks5Connection(sockfd);
}
