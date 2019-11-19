#include "PortForwarder.h"
#include <stdexcept>
#include <iostream>
#include <algorithm>
#include "InetUtils.h"

PortForwarder::PortForwarder(int lport, std::string rhost, int rport) : servSock(lport)
{
	redirectAddr = getAddr(rhost, rport);
}

void PortForwarder::run()
{
	std::cerr << "Forwarder started" << std::endl;
	constexpr int buffSize = 4096;
		char clientBuffer[buffSize],
			servBuffer[buffSize];
	while (true)
	{
		int clientSock = servSock.acceptConnection(),
			redirectedSock = openRedirectedSocket();

		int maxfd = std::max(clientSock, redirectedSock);
		std::cerr << "Client connected" << std::endl;
		bool clientEof = false,
			serverEof = false;
		
    	fd_set rdfds;
	    fd_set wrfds;

		while (!(clientEof && serverEof))
		{

			FD_ZERO(&rdfds);
			FD_ZERO(&wrfds);
			FD_SET(clientSock, &rdfds);
			FD_SET(clientSock, &wrfds);
			FD_SET(redirectedSock, &rdfds);
			FD_SET(redirectedSock, &wrfds);
			
			struct timeval tv;
			tv.tv_sec = 5;
			tv.tv_usec = 0;
			int selected = select(maxfd + 1, &rdfds, &wrfds, NULL, &tv);
			if (selected == 0)
				continue;

			if (!clientEof && FD_ISSET(clientSock, &rdfds))
			{
				int bytesRead = read(clientSock, clientBuffer, buffSize);
				if (bytesRead == 0)
				{
					clientEof = true;
				}
				else
				{
					write(redirectedSock, clientBuffer, bytesRead);
				}
				std::cerr << "Read " << bytesRead << " from client" << std::endl;
			}
			if (!serverEof && FD_ISSET(redirectedSock, &wrfds))
			{
				int bytesRead = read(redirectedSock, servBuffer, buffSize);
				if (bytesRead == 0)
				{
					serverEof = true;
				}
				else
				{
					write(clientSock, servBuffer, bytesRead);
				}
				std::cerr << "Read " << bytesRead << " from server" << std::endl;
			}

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