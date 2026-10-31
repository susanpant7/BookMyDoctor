using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BookMyDoctor.Features.Common;

// The request is fine but clashes with existing data (a name already taken, a record still in use).
public sealed class ConflictException(string message) : Exception(message);

// The outcome of loading default data: how many were added, and how many already existed and were left as they are.
public sealed record LoadDefaultsResult(int Added, int Skipped);

// Turns exceptions thrown by services into the { message } body the client shows.
public sealed class ServiceExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var status = context.Exception switch
        {
            ArgumentException => StatusCodes.Status400BadRequest,
            KeyNotFoundException => StatusCodes.Status404NotFound,
            ConflictException => StatusCodes.Status409Conflict,
            _ => 0
        };
        if (status == 0)
        {
            return;
        }

        context.Result = new ObjectResult(new { message = context.Exception.Message }) { StatusCode = status };
        context.ExceptionHandled = true;
    }
}
