#include "Socks5Proxy.h"
#include <stdexcept>
#include <iostream>
#include <algorithm>
#include <string.h>
#include "InetUtils.h"

Socks5Proxy::Socks5Proxy(int lport) : servSock(lport)
{
	maxfd = servSock.getFd();
}

void Socks5Proxy::run()
{
	std::cerr << "Forwarder started" << std::endl;

	bool removedAny = false;
	auto connectionComparator = [](Connection a, Connection b) -> bool
	{
		return a.getFd() < b.getFd();
	};

	auto socks5ConnComparator = [](Socks5Connection a, Socks5Connection b) -> bool
	{
		return a.getFd() < b.getFd();
	};

	while (true)
	{
		FD_ZERO(&readfs);
		FD_ZERO(&writefs);
		FD_SET(servSock.getFd(), &readfs);

		for (std::list<Socks5Connection>::iterator it = handshakingConns.begin(); it != handshakingConns.end(); ++it)
		{
			it->setFds(&readfs, &writefs);
		}
		for (std::list<Connection>::iterator it = proccessingConns.begin(); it != proccessingConns.end(); ++it)
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
			handshakingConns.emplace_back(clientSock);
			maxfd = std::max(maxfd, clientSock);
		}

		for (std::list<Socks5Connection>::iterator it = handshakingConns.begin(); it != handshakingConns.end(); ++it)
		{
			it->proccessConnection(&readfs, &writefs);
			if (!it->isActive())
			{
				if (it->isSuccseed())
				{
					int coupledSock = openRedirectedSocket(it->getAddr(), it->redirectedPort());
					proccessingConns.emplace_back(it->getFd(), coupledSock);
					proccessingConns.emplace_back(coupledSock, it->getFd());
				}

				std::list<Socks5Connection>::iterator backup = it--;
				handshakingConns.erase(backup);
				removedAny = true;
			}
		}

		for (std::list<Connection>::iterator it = proccessingConns.begin(); it != proccessingConns.end(); ++it)
		{
			it->proccessConnection(&readfs, &writefs);
			if (!it->isActive())
			{
				std::list<Connection>::iterator backup = it--;
				proccessingConns.erase(backup);
				removedAny = true;
			}
		}

		if (removedAny)
		{
			maxfd = servSock.getFd();
			int listsMax = std::max(
				std::max_element(proccessingConns.begin(), proccessingConns.end(), connectionComparator)->getFd(),
				std::max_element(handshakingConns.begin(), handshakingConns.end(), socks5ConnComparator)->getFd()
			);
			maxfd = std::max(listsMax, maxfd);
		}

	}
}


int Socks5Proxy::openRedirectedSocket(std::string addr, int port)
{
	struct sockaddr_in redirectAddr = getAddr(addr, port);
	int sock = socket(AF_INET, SOCK_STREAM, 0);
	if (sock == -1 || connect(sock, (struct sockaddr*)&redirectAddr, sizeof(redirectAddr)))
	{
		throw std::runtime_error("redirecting failed");
	}
	return sock;
}