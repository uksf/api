using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using UKSF.Api.Core.Models;

namespace UKSF.Api.Filters;

public class ValidateModelStateFilter : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.ModelState.IsValid)
        {
            return;
        }

        var errors = context.ModelState.Where(x => x.Value?.Errors.Count > 0).SelectMany(x => x.Value!.Errors.Select(e => e.ErrorMessage)).ToList();

        var message = string.Join("; ", errors);
        context.Result = new BadRequestObjectResult(new UksfErrorMessage(400, 0, message, null));
    }
}
