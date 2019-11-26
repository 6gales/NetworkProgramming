#pragma once
#include <string>
#include "netdb.h"

struct sockaddr_in getAddr(std::string host, int port);