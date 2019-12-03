#pragma once
#include <netdb.h>
#include <vector>
#include <stdexcept>
#include <algorithm>

class FdRegistrar
{
	fd_set readfs,
		writefs;

	std::vector<int> subscribedFds;

public:
	FdRegistrar()
	{
		FD_ZERO(&readfs);
		FD_ZERO(&writefs);
	}

	void registerFd(int fd)
	{
		subscribedFds.push_back(fd);
	}

	int selectFds()
	{
		int selected;
		do
		{
			int maxfd = -1;
			for (size_t i = 0; i < subscribedFds.size(); i++)
			{
				FD_SET(subscribedFds[i], &readfs);
				FD_SET(subscribedFds[i], &writefs);
				maxfd = std::max(maxfd, subscribedFds[i]);
			}

			if (maxfd == -1)
				throw std::runtime_error("no file descriptors to select from");

			selected = select(maxfd + 1, &readfs, &writefs, NULL, NULL);

		} while (selected == 0);

		subscribedFds.clear();

		return selected;
	}

	bool isSetRead(int fd)
	{
		return FD_ISSET(fd, &readfs);
	}

	bool isSetWrite(int fd)
	{
		return FD_ISSET(fd, &readfs);
	}
};