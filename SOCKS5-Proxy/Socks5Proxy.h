#pragma once

#include <string>
#include <netdb.h>
#include <list>
#include "ServerSocket.h"
#include "Connection.h"
#include "Socks5Connection.h"

class Socks5Proxy
{
	const ServerSocket servSock;
	fd_set readfs,
		writefs;
	int maxfd;
	std::list<Socks5Connection> handshakingConns;
	std::list<Connection> proccessingConns;

	int openRedirectedSocket(std::string addr, int port);

public:
	Socks5Proxy(int lport);

	void run();
};