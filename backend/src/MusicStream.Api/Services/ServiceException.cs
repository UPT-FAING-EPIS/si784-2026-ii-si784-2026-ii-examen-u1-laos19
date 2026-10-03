namespace MusicStream.Api.Services;

/// <summary>Error de negocio que se traduce a una respuesta HTTP con ProblemDetails.</summary>
public class ServiceException : Exception
{
    public ServiceException(int statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }

    public static ServiceException NotFound(string message) => new(StatusCodes.Status404NotFound, message);
    public static ServiceException Conflict(string message) => new(StatusCodes.Status409Conflict, message);
    public static ServiceException Forbidden(string message) => new(StatusCodes.Status403Forbidden, message);
    public static ServiceException BadRequest(string message) => new(StatusCodes.Status400BadRequest, message);
    public static ServiceException Unauthorized(string message) => new(StatusCodes.Status401Unauthorized, message);
}
