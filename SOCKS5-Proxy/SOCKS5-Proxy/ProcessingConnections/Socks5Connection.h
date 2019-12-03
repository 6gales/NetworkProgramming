#pragma once

#include <string>
#include <unistd.h>
#include <sys/types.h>
#include "../Socks5Context.h"

class Socks5Connection : public Socks5Context::ProcessingState
{
	static constexpr unsigned char
		socksVersion        = 0x05,
		accepted            = 0x00,
		reserved            = 0x00,
		noAuthentication    = 0x00,
		noAcceptableMethods = 0xff,
		connectCommand      = 0x01,
		ipv4                = 0x01,
		domainName          = 0x03,
		commandNotSupported = 0x07,
		addressNotSupported = 0x08;


	constexpr static std::size_t BUFF_SIZE = 1024;
	unsigned char sendBuffer[BUFF_SIZE],
		recvBuffer[BUFF_SIZE];

	bool initialStage = true,
		needWrite = false,
		eof = false,
		domain = false,
		succeed = false;
	
	std::string addr;
	
	size_t sendOffset = 0,
		recvOffset = 0,
		sendSize = 0;
	int port = 0;

	const int clientFd;

	bool performFirstStage();
	bool negotiate();

public:
	Socks5Connection(int _clientFd) : clientFd(_clientFd) {}

	ProcessingState* process(Socks5Context* context) override;

	~Socks5Connection()
	{
		if (!succeed)
		{
			close(clientFd);
		}
	}
};
