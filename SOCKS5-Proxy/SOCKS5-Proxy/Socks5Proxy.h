#pragma once

#include <string>
#include <netdb.h>
#include <list>
#include <vector>
#include "ServerSocket.h"
#include "FdRegistrar.hpp"
#include "Socks5Context.h"

class Socks5Proxy
{
	const ServerSocket servSock;

	FdRegistrar registrar;

	std::list<Socks5Context*> contexts;

public:
	Socks5Proxy(int lport) : servSock(lport) {}

	void run();
};