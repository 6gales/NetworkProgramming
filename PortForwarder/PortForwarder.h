#pragma once

#include <string>
#include <netdb.h>
#include <list>
#include "ServerSocket.h"
#include "Connection.h"

class PortForwarder
{
	const ServerSocket servSock;
	struct sockaddr_in redirectAddr;
	fd_set readfs,
		writefs;
	int maxfd;
	std::list<Connection> conns;

	int openRedirectedSocket();

public:
	PortForwarder(int lport, std::string rhost, int rport);

	void run();
};