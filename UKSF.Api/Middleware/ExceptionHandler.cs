using System.Text.Json.Serialization;
using UKSF.Api.Shared.Exceptions;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.Middleware;

public interface IExceptionHandler
{
    Task Handle(Exception exception, HttpContext context);
}

public class ExceptionHandler : IExceptionHandler
{
    public async Task Handle(Exception exception, HttpContext context)
    {
        switch (exception)
        {
            case UksfException uksfException:
                await HandleUksfException(uksfException, context);
                break;
            default:
                await HandleUnhandledException(exception, context);
                break;
        }

        await context.Response.Body.FlushAsync();
    }

    private static Task HandleUnhandledException(Exception exception, HttpContext context)
    {
        if (context.Response.StatusCode < 300)
        {
            context.Response.StatusCode = 500;
        }

        Console.Out.WriteLine(exception.ToString());
        Console.Out.WriteLine(exception.InnerException?.ToString());

        context.Response.ContentType = "application/json";
        return SerializeToStream(context.Response, new(context.Response.StatusCode, 0, $"{exception.Message}\n{exception.InnerException?.Message}", null));
    }

    private static Task HandleUksfException(UksfException uksfException, HttpContext context)
    {
        context.Response.StatusCode = uksfException.StatusCode;
        context.Response.ContentType = "application/json";

        return SerializeToStream(context.Response, new(uksfException.StatusCode, uksfException.DetailCode, uksfException.Message, uksfException.Validation));
    }

    private static Task SerializeToStream(HttpResponse response, UksfErrorMessage error)
    {
        return response.WriteAsJsonAsync(
            error,
            new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
        );
    }
}
