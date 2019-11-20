#include "PortForwarder.h"
#include <stdexcept>
#include <iostream>
#include <algorithm>
#include <string.h>
#include "InetUtils.h"

PortForwarder::PortForwarder(int lport, std::string rhost, int rport) : servSock(lport)
{
	redirectAddr = getAddr(rhost, rport);
	maxfd = servSock.getFd();
}

void PortForwarder::run()
{
	std::cerr << "Forwarder started" << std::endl;

	bool removedAny = false;

	while (true)
	{
		FD_ZERO(&readfs);
		FD_ZERO(&writefs);
		FD_SET(servSock.getFd(), &readfs);
		for (std::list<Connection>::iterator it = conns.begin(); it != conns.end(); ++it)
		{
			it->setFds(&readfs, &writefs);
		}
			
		int selected = select(maxfd + 1, &readfs, &writefs, NULL, NULL);
		if (selected < 0)
		{
			throw std::runtime_error(std::string("select: ") + strerror(errno));
		}
		if (selected == 0)
			continue;
		
		if (FD_ISSET(servSock.getFd(), &readfs))
		{
			int clientSock = servSock.acceptConnection();
			int coupledSock = openRedirectedSocket();
			conns.emplace_back(clientSock, coupledSock);
			conns.emplace_back(coupledSock, clientSock);
			maxfd = std::max(maxfd, std::max(clientSock, coupledSock));
		}

		for (std::list<Connection>::iterator it = conns.begin(); it != conns.end(); ++it)
		{
			it->proccessConnection(&readfs, &writefs);
			if (!it->isActive())
			{
				std::list<Connection>::iterator backup = it--;
				conns.erase(backup);
				removedAny = true;
			}
		}

		if (removedAny)
		{
			maxfd = std::max(servSock.getFd(), std::max_element(conns.begin(), conns.end(), [](Connection a, Connection b) -> bool
			{
				return a.getFd() < b.getFd();
			})->getFd());
		}

	}
}

int PortForwarder::openRedirectedSocket()
{
	int sock = socket(AF_INET, SOCK_STREAM, 0);
	if (sock == -1 || connect(sock, (struct sockaddr*)&redirectAddr, sizeof(redirectAddr)))
	{
		throw std::runtime_error("redirecting failed");
	}
	return sock;
}