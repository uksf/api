using System;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using UKSF.Api.Interfaces.Personnel;
using UKSF.Api.Interfaces.Utility;
using UKSF.Api.Models.Message.Logging;
using UKSF.Api.Services.Message;

namespace UKSF.Api.Services {
    public class ExceptionHandler : IExceptionFilter {
        private readonly IDisplayNameService displayNameService;
        private readonly ISessionService sessionService;

        public ExceptionHandler(ISessionService sessionService, IDisplayNameService displayNameService) {
            this.sessionService = sessionService;
            this.displayNameService = displayNameService;
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
                Log(context, filterContext.Exception);
                filterContext.ExceptionHandled = true;
                filterContext.Result = new ContentResult {Content = $"{filterContext.Exception.Message}", ContentType = "text/plain", StatusCode = (int?) HttpStatusCode.BadRequest};
            }
        }

        private void Log(HttpContext context, Exception exception) {
            bool authenticated = context != null && context.User.Identity.IsAuthenticated;
            WebLogMessage logMessage = new WebLogMessage(exception) {
                httpMethod = context?.Request.Method ?? string.Empty, url = context?.Request.GetDisplayUrl(), userId = authenticated ? sessionService.GetContextId() : "GUEST", name = authenticated ? displayNameService.GetDisplayName(sessionService.GetContextAccount()) : "GUEST"
            };
            LogWrapper.Log(logMessage);
        }
    }
}
