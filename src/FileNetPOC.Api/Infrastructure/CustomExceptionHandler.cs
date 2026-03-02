using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using FluentValidation;

namespace FileNetPOC.Api.Infrastructure;

public class CustomExceptionHandler : IExceptionHandler
{
    private readonly ILogger<CustomExceptionHandler> _logger;

    public CustomExceptionHandler(ILogger<CustomExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Exception occurred: {Message}", exception.Message);

        var problemDetails = new ProblemDetails
        {
            Instance = httpContext.Request.Path
        };

        if (exception is ValidationException validationException)
        {
            problemDetails.Title = "Validation Error";
            problemDetails.Status = StatusCodes.Status400BadRequest;
            problemDetails.Detail = "One or more validation errors occurred.";
            
            // Groups the FluentValidation errors nicely by property name
            problemDetails.Extensions["errors"] = validationException.Errors
                .GroupBy(x => x.PropertyName, x => x.ErrorMessage)
                .ToDictionary(x => x.Key, x => x.ToArray());
        }
        else
        {
            problemDetails.Title = "An unexpected error occurred";
            problemDetails.Status = StatusCodes.Status500InternalServerError;
            // Note: In a true production environment, you might hide exception.Message 
            // from the client for security, but it's great for our POC debugging.
            problemDetails.Detail = exception.Message; 
        }

        httpContext.Response.StatusCode = problemDetails.Status.Value;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        // Returning true tells the .NET pipeline that we successfully handled the crash
        return true; 
    }
}