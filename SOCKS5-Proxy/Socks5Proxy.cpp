#include "Socks5Proxy.h"
#include <stdexcept>
#include <iostream>
#include <algorithm>
#include <string.h>
#include <fcntl.h>
#include "InetUtils.h"

Socks5Proxy::Socks5Proxy(int lport) : servSock(lport)
{
	maxfd = servSock.getFd();
}

void Socks5Proxy::run()
{
	std::cerr << "Forwarder started" << std::endl;

	bool removedAny = false;
	auto fd_is_valid = [](int fd) -> int
	{
		return fcntl(fd, F_GETFD) != -1 || errno != EBADF;
	};
	auto connectionComparator = [&fd_is_valid](Connection a, Connection b) -> bool
	{
		fprintf(stderr, "%d is valid: %d, %d is valid: %d\n",
				a.getFd(), fd_is_valid(a.getFd()), b.getFd(), fd_is_valid(b.getFd()));
		return a.getFd() < b.getFd();
	};

	auto socks5ConnComparator = [&fd_is_valid](Socks5Connection a, Socks5Connection b) -> bool
	{
		fprintf(stderr, "%d is valid: %d, %d is valid: %d\n",
				a.getFd(), fd_is_valid(a.getFd()), b.getFd(), fd_is_valid(b.getFd()));
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

		for (auto it = resolvingConns.begin(); it != resolvingConns.end(); ++it)
		{
			it->second->setFds(readfs, writefs);
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
			//if (!it->isActive())
			if (it->isSuccseed())
			{
				if (!it->isDomain())
				{
					int coupledSock = openRedirectedSocket(it->getAddr(), it->redirectedPort());
					FD_CLR(it->getFd(), &readfs);
					FD_CLR(it->getFd(), &writefs);
					proccessingConns.emplace_back(it->getFd(), coupledSock);
					proccessingConns.emplace_back(coupledSock, it->getFd());
				}
				else
				{
					std::pair<int, int> fdPort{ it->getFd(), it->redirectedPort() };
					resolvingConns.emplace_back(fdPort, new DnsResolver((unsigned char *) it->getAddr().c_str()));
				}

				std::list<Socks5Connection>::iterator backup = it--;
				handshakingConns.erase(backup);
				removedAny = true;
			}
		}

		for (auto it = resolvingConns.begin(); it != resolvingConns.end(); ++it)
		{
			if (it->second->exchange_data(readfs, writefs))
			{
				fprintf(stderr, "Resolved domain\n");

				struct sockaddr_in addr;
				memset(&addr, 0, sizeof(addr));
				addr.sin_addr.s_addr = it->second->getaddr();
				addr.sin_port = htons(it->first.second);
				addr.sin_family = AF_INET;

				int sock = socket(AF_INET, SOCK_STREAM, 0);
				if (sock == -1 || connect(sock, (struct sockaddr*)&addr, sizeof(addr)))
				{
					throw std::runtime_error("redirecting failed");
				}

				proccessingConns.emplace_back(it->first.first, sock);
				proccessingConns.emplace_back(sock, it->first.first);


				maxfd = std::max(maxfd, sock);
				delete(it->second);
				resolvingConns.erase(it++);
			}
			else
			{
				fprintf(stderr, "Domain not resolved\n");

				maxfd = std::max(maxfd, it->second->getFd());
				it++;
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

//			auto maxP = std::max_element(proccessingConns.begin(), proccessingConns.end(), connectionComparator);
//			auto maxH = std::max_element(handshakingConns.begin(), handshakingConns.end(), socks5ConnComparator);

			int listsMax = -1;
//			if (maxP != proccessingConns.end() && maxH != handshakingConns.end())
//			{
//				listsMax = std::max(
//						maxP->getFd(),
//						maxH->getFd()
//				);
//			}
//			else if (maxP != proccessingConns.end())
//			{
//				listsMax = maxP->getFd();
//			}
//			else if (maxH != handshakingConns.end())
//			{
//				listsMax = maxH->getFd();
//			}

			for (std::list<Socks5Connection>::iterator it = handshakingConns.begin(); it != handshakingConns.end(); ++it)
			{
				if (it->getFd() > listsMax)
				{
					listsMax = it->getFd();
				}
			}

			for (std::list<Connection>::iterator it = proccessingConns.begin(); it != proccessingConns.end(); ++it)
			{
				if (it->getFd() > listsMax)
				{
					listsMax = it->getFd();
				}
			}

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