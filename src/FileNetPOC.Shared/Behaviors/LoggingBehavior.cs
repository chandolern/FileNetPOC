using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FileNetPOC.Shared.Behaviors;

public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        
        _logger.LogInformation("Handling Command/Query: {RequestName}", requestName);
        
        var timer = Stopwatch.StartNew();
        
        // This executes the actual handler (or the next behavior in the pipeline)
        var response = await next();
        
        timer.Stop();
        
        _logger.LogInformation("Handled Command/Query: {RequestName} in {ElapsedMilliseconds} ms", requestName, timer.ElapsedMilliseconds);
        
        return response;
    }
}