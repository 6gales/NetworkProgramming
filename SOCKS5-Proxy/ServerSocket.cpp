#include "ServerSocket.h"
#include <sys/types.h>
#include <sys/socket.h>
#include <string.h>
#include <unistd.h>
#include <fcntl.h>
#include <stdexcept>
#include "InetUtils.h"

ServerSocket::ServerSocket(int port) : sockfd(socket(AF_INET, SOCK_STREAM, 0))
{
	if (sockfd == -1)
	{
		throw std::runtime_error(std::string("socket: ") + strerror(errno));
	}
	if (sockfd >= FD_SETSIZE)
	{
		throw std::runtime_error(std::string("socket fd is out of select range"));
	}
	
	int opt = 1;
	setsockopt(sockfd, SOL_SOCKET, SO_REUSEADDR, &opt, sizeof(opt));
    struct sockaddr_in listenaddr = getAddr("", port);

	if (bind(sockfd, reinterpret_cast<struct sockaddr *>(&listenaddr), sizeof(listenaddr)))
	{
		throw std::runtime_error(std::string("bind: ") + strerror(errno));
	}

	if (listen(sockfd, SOMAXCONN))
	{
		throw std::runtime_error(std::string("listen: ") + strerror(errno));
	}
	if (fcntl(sockfd, F_SETFL, fcntl(sockfd, F_GETFL, 0) | O_NONBLOCK) == -1)
	{
		throw std::runtime_error("fcnt: cannot make server socket nonblock");
	}
}

int ServerSocket::acceptConnection() const
{
	sockaddr_in clientAddr;
	socklen_t addrlen = sizeof(clientAddr);
	int clientSock = ::accept(sockfd, reinterpret_cast<sockaddr *>(&clientAddr), &addrlen);
	if (clientSock < 0)
	{
		throw std::runtime_error(std::string("accept: ") + strerror(errno));
	}
	fcntl(clientSock, F_SETFL, fcntl(clientSock, F_GETFL, 0) | O_NONBLOCK);
	return clientSock;
}