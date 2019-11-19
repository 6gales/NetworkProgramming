#pragma once

#include <string>
#include <netdb.h>
#include "ServerSocket.h"

class PortForwarder
{
	const ServerSocket servSock;
	struct sockaddr_in redirectAddr;

	int openRedirectedSocket();

public:
	PortForwarder(int lport, std::string rhost, int rport);

	void run();
};