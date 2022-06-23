using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Exceptions;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.Middleware
{
    public class ExceptionMiddleware : IMiddleware
    {
        private readonly IDisplayNameService _displayNameService;
        private readonly IExceptionHandler _exceptionHandler;
        private readonly IHttpContextService _httpContextService;
        private readonly ILogger _logger;

        public ExceptionMiddleware(ILogger logger, IDisplayNameService displayNameService, IHttpContextService httpContextService, IExceptionHandler exceptionHandler)
        {
            _logger = logger;
            _displayNameService = displayNameService;
            _httpContextService = httpContextService;
            _exceptionHandler = exceptionHandler;
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            if (context == null)
            {
                return;
            }

            if (context.Request.Method == HttpMethod.Options.Method)
            {
                await next(context);
                return;
            }

            Exception exception = null;
            try
            {
                await next(context);
            }
            catch (Exception ex)
            {
                exception = ex;
            }
            finally
            {
                if (exception is UksfException uksfException)
                {
                    await HandleError(context, uksfException);
                }
                else if (exception != null)
                {
                    await HandleError(context, exception);
                }
                else if (IsError(context.Response))
                {
                    exception = GetException(context);
                    await HandleError(context, exception);
                }
            }
        }

        private async Task HandleError(HttpContext context, Exception exception)
        {
            if (!context.Response.HasStarted)
            {
                await _exceptionHandler.Handle(exception, context);
            }

            var authenticated = _httpContextService.IsUserAuthenticated();
            var userId = _httpContextService.GetUserId();
            var userDisplayName = authenticated ? _displayNameService.GetDisplayName(userId) : "Guest";
            _logger.LogError(exception, context, context.Response, authenticated ? userId : "Guest", userDisplayName);
        }

        private static bool IsError(HttpResponse response)
        {
            return response is { StatusCode: >= 400 };
        }

        private static Exception GetException(HttpContext context)
        {
            context.Items.TryGetValue("exception", out var exception);

            return exception as Exception ?? new Exception("unknown error");
        }
    }
}
