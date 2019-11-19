#pragma once
#include <unistd.h>

class ServerSocket
{
	const int sockfd;
	
public:
	ServerSocket(int port);

	int acceptConnection() const;

	int getFd() const
	{
		return sockfd;
	}

	~ServerSocket()
	{
		close(sockfd);
	}
};