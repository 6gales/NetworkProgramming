#pragma once
#include <string>
#include "netdb.h"

struct sockaddr_in getAddr(std::string host, int port);

struct sockaddr_in getAddr(struct addrinfo* addr, int port);

int openRedirectedSocket(struct sockaddr_in sockAddr);

void fillHints(struct addrinfo* hints);