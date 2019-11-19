#include <iostream>
#include <cstdlib>
#include "PortForwarder.h"

int main(int argc, char **argv)
{
	if (argc < 3)
	{
		std::cerr << "Usage: <lport> <rhost> <rport>" << std::endl;
		return 0;
	}

	int lport = std::atoi(argv[1]),
		rport = (argc > 3 ? std::atoi(argv[3]) : 80);
	
	if (lport == 0 || rport == 0)
	{
		std::cerr << "Wrong port number" << std::endl;
		return 1;
	}

	
	PortForwarder forwarder{ lport, std::string{ argv[2] }, rport };
	forwarder.run();
}