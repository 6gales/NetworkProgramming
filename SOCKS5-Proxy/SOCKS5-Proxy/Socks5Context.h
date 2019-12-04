#pragma once
#include "FdRegistrar.hpp"

class Socks5Context
{
public:
	class ProcessingState
	{
	public:
		virtual ProcessingState* process(Socks5Context* context) = 0;

		virtual ~ProcessingState() {}
	};

private:
	ProcessingState* state;
	FdRegistrar& registrar;
	bool finished = false;

public:
	Socks5Context(int sockfd, FdRegistrar& _registrar);

	void process()
	{
		ProcessingState* newState = state->process(this);
		if (newState != nullptr)
		{
			delete state;
			state = newState;
		}
	}

	void finish() { finished = true; }

	bool isFinished() const { return finished; }
	
	FdRegistrar& getRegistrar() const
	{
		return registrar;
	}
	
	~Socks5Context()
	{
		delete state;
		state = nullptr;
	}
};

