#include <string>
#include <unistd.h>
#include <sys/types.h>
#include <sys/socket.h>

class Socks5Connection
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


	constexpr static size_t BUFF_SIZE = 1024;
	unsigned char sendBuffer[BUFF_SIZE],
		recvBuffer[BUFF_SIZE];

	bool initialStage = true,
		needWrite = false,
		eof = false,
		domain = false,
		succseed = false;
	
	std::string addr;
	
	int sendOffset = 0,
		recvOffset = 0,
		sendSize = 0,
		port = 0;

	const int clientFd;

	bool performFirstStage();
	bool proccessData();

public:
	Socks5Connection(int _clientFd) : clientFd(_clientFd) {}

	void proccessConnection(fd_set *readfds, fd_set *writefds);

	bool isActive()
	{
		return !eof || initialStage || needWrite;
	}

	bool isDomain()
	{
		return domain;
	}

	bool isSuccseed()
	{
		return succseed;
	}

	int getFd()
	{
		return clientFd;
	}

	std::string getAddr()
	{
		return addr;
	}

	int redirectedPort()
	{
		return port;
	}

	void setFds(fd_set *readfds, fd_set *writefds)
	{
		if (!eof)
		{
			FD_SET(clientFd, readfds);
		}
		FD_SET(clientFd, writefds); 
	}

	~Socks5Connection()
	{
		if (!succseed)
		{
			fprintf(stderr, "closing %d\n", clientFd);
			close(clientFd);
		}
	}
};