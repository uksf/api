using System;
using System.IO.Pipelines;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using UKSF.Api.Shared.Exceptions;
using UKSF.Api.Shared.Models;
using Utf8Json;
using Utf8Json.Resolvers;

namespace UKSF.Api.Middleware
{
    public interface IExceptionHandler
    {
        ValueTask Handle(Exception exception, HttpContext context);
    }

    public class ExceptionHandler : IExceptionHandler
    {
        public async ValueTask Handle(Exception exception, HttpContext context)
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

            await context.Response.BodyWriter.FlushAsync();
        }

        private static ValueTask<FlushResult> HandleUnhandledException(Exception exception, HttpContext context)
        {
            if (context.Response.StatusCode < 300)
            {
                context.Response.StatusCode = 500;
            }

            context.Response.ContentType = "application/json";
            return SerializeToStream(context.Response.BodyWriter, new(context.Response.StatusCode, 0, exception?.Message, null));
        }

        private static ValueTask<FlushResult> HandleUksfException(UksfException uksfException, HttpContext context)
        {
            context.Response.StatusCode = uksfException.StatusCode;
            context.Response.ContentType = "application/json";

            return SerializeToStream(context.Response.BodyWriter, new(uksfException.StatusCode, uksfException.DetailCode, uksfException.Message, uksfException.Validation));
        }

        private static ValueTask<FlushResult> SerializeToStream(PipeWriter output, UksfErrorMessage error)
        {
            return output.WriteAsync(JsonSerializer.Serialize(error, StandardResolver.AllowPrivateExcludeNullCamelCase));
        }
    }
}
