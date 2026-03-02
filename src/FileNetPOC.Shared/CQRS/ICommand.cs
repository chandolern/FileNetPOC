using MediatR;

namespace FileNetPOC.Shared.CQRS;

public interface ICommand : ICommand<Unit>
{
}

public interface ICommand<out TResponse> : IRequest<TResponse>
{
}

// Handlers must be invariant to match MediatR v12+
public interface ICommandHandler<TCommand> 
    : ICommandHandler<TCommand, Unit>
    where TCommand : ICommand<Unit>
{ 
}

public interface ICommandHandler<TCommand, TResponse> 
    : IRequestHandler<TCommand, TResponse> 
    where TCommand : ICommand<TResponse>
    where TResponse : notnull
{
}