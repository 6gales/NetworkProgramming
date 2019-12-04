#include "Socks5Proxy.h"
#include <stdexcept>
#include <iostream>
#include <algorithm>
#include <string.h>
#include <fcntl.h>
#include "InetUtils.h"

void Socks5Proxy::run()
{
	std::cerr << "Proxy started" << std::endl;

	while (true)
	{
		registrar.registerFd(servSock.getFd());
		
		if (registrar.selectFds() < 0)
		{
			throw std::runtime_error(std::string("select: ") + strerror(errno));
		}
		
		if (registrar.isSetRead(servSock.getFd()))
		{
			fprintf(stderr, "New client connected\n");
			int clientSock = servSock.acceptConnection();

			contexts.push_back(new Socks5Context(clientSock, registrar));
		}

		for (auto it = contexts.begin(); it != contexts.end(); ++it)
		{
			(*it)->process();
			if ((*it)->isFinished())
			{
				delete *it;
				contexts.erase(it--);
			}
		}
	}
}