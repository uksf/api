using System;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Models;
using UKSF.Api.Shared.Services;

namespace UKSF.Api {
    public class ExceptionHandler : IExceptionFilter {
        private readonly IDisplayNameService _displayNameService;
        private readonly IHttpContextService _httpContextService;
        private readonly ILogger _logger;

        public ExceptionHandler(IDisplayNameService displayNameService, IHttpContextService httpContextService, ILogger logger) {
            _displayNameService = displayNameService;
            _httpContextService = httpContextService;
            _logger = logger;
        }

        public void OnException(ExceptionContext filterContext) {
            if (filterContext == null) throw new ArgumentNullException(nameof(filterContext));

            if (filterContext.Exception is NotImplementedException) {
                // not implemented exception - not logged
                filterContext.Result = new OkObjectResult(null);
                filterContext.ExceptionHandled = true;
            } else {
                // unhandled/unexpected exception - log always
                HttpContext context = filterContext.HttpContext;
                LogError(context, filterContext.Exception);
                filterContext.ExceptionHandled = true;
                filterContext.Result = new ContentResult { Content = $"{filterContext.Exception.Message}", ContentType = "text/plain", StatusCode = (int?) HttpStatusCode.BadRequest };
            }
        }

        private void LogError(HttpContext context, Exception exception) {
            bool authenticated = _httpContextService.IsUserAuthenticated();
            string userId = _httpContextService.GetUserId();
            HttpErrorLog log = new(
                exception,
                authenticated ? _displayNameService.GetDisplayName(userId) : "Guest",
                authenticated ? userId : "Guest",
                context?.Request.Method ?? string.Empty,
                context?.Request.GetDisplayUrl()
            );
            _logger.LogHttpError(log);
        }
    }
}
