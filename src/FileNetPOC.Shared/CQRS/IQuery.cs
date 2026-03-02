using MediatR;

namespace FileNetPOC.Shared.CQRS;

public interface IQuery<out TResponse> : IRequest<TResponse>  
    where TResponse : notnull
{
}

// Handlers must be invariant to match MediatR v12+
public interface IQueryHandler<TQuery, TResponse>
    : IRequestHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>
    where TResponse : notnull
{
}