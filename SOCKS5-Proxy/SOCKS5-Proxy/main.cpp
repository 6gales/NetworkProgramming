#include <iostream>
#include <cstdlib>
#include "Socks5Proxy.h"

int main(int argc, char **argv)
{
	int lport = (argc > 1 ? std::atoi(argv[1]) : 1080);
	
	if (lport == 0)
	{
		std::cerr << "Usage: <lport>" << std::endl;
		return 1;
	}
	
	Socks5Proxy proxy{ lport };
	proxy.run();
}