using System;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Models.Logging;
using UKSF.Api.Base.Services;
using UKSF.Api.Personnel.Services;

namespace UKSF.Api {
    public class ExceptionHandler : IExceptionFilter {
        private readonly IDisplayNameService displayNameService;
        private readonly IHttpContextService httpContextService;
        private readonly ILogger logger;

        public ExceptionHandler(IDisplayNameService displayNameService, IHttpContextService httpContextService, ILogger logger) {
            this.displayNameService = displayNameService;
            this.httpContextService = httpContextService;
            this.logger = logger;
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
            bool authenticated = httpContextService.IsUserAuthenticated();
            string userId = httpContextService.GetUserId();
            HttpErrorLog log = new HttpErrorLog(exception) {
                httpMethod = context?.Request.Method ?? string.Empty,
                url = context?.Request.GetDisplayUrl(),
                userId = authenticated ? userId : "Guest",
                name = authenticated ? displayNameService.GetDisplayName(userId) : "Guest"
            };
            logger.LogHttpError(log);
        }
    }
}
