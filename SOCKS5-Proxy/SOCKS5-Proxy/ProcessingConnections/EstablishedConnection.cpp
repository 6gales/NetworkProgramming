#include "EstablishedConnection.h"
#include <cstring>

Socks5Context::ProcessingState* EstablishedConnection::process(Socks5Context* context)
{
	exchangeData(context, clientSock, servSock, clientBuffer);
	exchangeData(context, servSock, clientSock, servBuffer);

	if (clientBuffer.isFinished() && servBuffer.isFinished())
	{
		context->finish();
	}
	else
	{
		context->getRegistrar().registerFd(clientSock);
		context->getRegistrar().registerFd(servSock);
	}

	return nullptr;
}

void EstablishedConnection::exchangeData(Socks5Context* context, int fromFd, int toFd, ConnectionBuffer &buffer)
{
	auto d = [](std::string s, bool b) -> bool { fprintf(stderr, "%s:%d\n", s.c_str(), b); return b; };
	if (d("full read:",d("is read from:" ,context->getRegistrar().isSetRead(fromFd)) && !buffer.isFull() && !buffer.eof))
	{
		ssize_t bytesRead = recv(fromFd, buffer.getEmptyData(), buffer.countEmpty(), 0);
		if (bytesRead < 0)
		{
			throw std::runtime_error(std::string("recv: ") + strerror(errno));
		}
		if (0 == bytesRead)
		{
			buffer.eof = true;
		}
		else
		{
			buffer.addRead(bytesRead);
		}
	}
	if (d("full write", d("is write to:",context->getRegistrar().isSetWrite(toFd)) && buffer.hasData()))
	{
		ssize_t bytesWrote = send(toFd, buffer.getUnread(), buffer.countUnread(), 0);
		if (bytesWrote < 0 && bytesWrote != EWOULDBLOCK)
		{
			throw std::runtime_error(std::string("send: ") + strerror(errno));
		}
		if (bytesWrote > 0)
			buffer.addWrote(bytesWrote);

		if (buffer.isFull() && !buffer.hasData())
		{
			buffer.reset();
		}
	}
}