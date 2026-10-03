using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MusicStream.Api.Services;

namespace MusicStream.Api.Infrastructure;

/// <summary>Convierte <see cref="ServiceException"/> en respuestas ProblemDetails sin tratarlas como errores no controlados.</summary>
public sealed class ServiceExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not ServiceException serviceException)
        {
            return;
        }

        var problem = new ProblemDetails
        {
            Status = serviceException.StatusCode,
            Title = serviceException.Message,
            Instance = context.HttpContext.Request.Path
        };
        context.Result = new ObjectResult(problem) { StatusCode = serviceException.StatusCode };
        context.ExceptionHandled = true;
    }
}
