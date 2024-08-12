using System.IO.Pipelines;
using System.Net;
using System.Text.Json.Serialization;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models;

namespace UKSF.Api.Middleware;

public interface IExceptionHandler
{
    Task Handle(Exception exception, HttpContext context);
}

public class ExceptionHandler : IExceptionHandler
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

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

        if (exception is not null)
        {
            Console.Out.WriteLine(exception.GetCompleteString());
        }

        context.Response.ContentType = "application/json";
        var message = exception?.GetCompleteMessage() ?? ((HttpStatusCode)context.Response.StatusCode).ToString();
        return SerializeToStream(context.Response.BodyWriter, new UksfErrorMessage(context.Response.StatusCode, 0, message, null));
    }

    private static Task HandleUksfException(UksfException uksfException, HttpContext context)
    {
        context.Response.StatusCode = uksfException.StatusCode;
        context.Response.ContentType = "application/json";

        return SerializeToStream(
            context.Response.BodyWriter,
            new UksfErrorMessage(uksfException.StatusCode, uksfException.DetailCode, uksfException.Message, uksfException.Validation)
        );
    }

    private static Task SerializeToStream(PipeWriter output, UksfErrorMessage error)
    {
        return output.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(error, JsonSerializerOptions)).AsTask();
    }
}
