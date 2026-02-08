using System.Collections.Generic;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using UKSF.Api.Core.Models;
using UKSF.Api.Filters;
using Xunit;

namespace UKSF.Api.Tests.Filters;

public class ValidateModelStateFilterTests
{
    private readonly ValidateModelStateFilter _subject = new();

    private static ActionExecutingContext CreateContext(ModelStateDictionary modelState)
    {
        var actionContext = new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor(), modelState);
        return new ActionExecutingContext(actionContext, new List<IFilterMetadata>(), new Dictionary<string, object>(), null);
    }

    [Fact]
    public void OnActionExecuting_ShouldNotSetResult_WhenModelStateIsValid()
    {
        var context = CreateContext(new ModelStateDictionary());

        _subject.OnActionExecuting(context);

        context.Result.Should().BeNull();
    }

    [Fact]
    public void OnActionExecuting_ShouldReturnBadRequest_WhenModelStateIsInvalid()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Email", "The Email field is required.");
        var context = CreateContext(modelState);

        _subject.OnActionExecuting(context);

        context.Result.Should().BeOfType<BadRequestObjectResult>();
        var result = context.Result as BadRequestObjectResult;
        result!.StatusCode.Should().Be(400);
    }

    [Fact]
    public void OnActionExecuting_ShouldReturnUksfErrorMessage_WhenModelStateIsInvalid()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Email", "The Email field is required.");
        var context = CreateContext(modelState);

        _subject.OnActionExecuting(context);

        var result = context.Result as BadRequestObjectResult;
        var error = result!.Value as UksfErrorMessage;
        error.Should().NotBeNull();
        error!.StatusCode.Should().Be(400);
        error.DetailCode.Should().Be(0);
        error.Error.Should().Contain("The Email field is required.");
    }

    [Fact]
    public void OnActionExecuting_ShouldCombineMultipleErrors()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Email", "The Email field is required.");
        modelState.AddModelError("Password", "The Password field is required.");
        var context = CreateContext(modelState);

        _subject.OnActionExecuting(context);

        var result = context.Result as BadRequestObjectResult;
        var error = result!.Value as UksfErrorMessage;
        error!.Error.Should().Contain("Email");
        error.Error.Should().Contain("Password");
    }

    [Fact]
    public void OnActionExecuting_ShouldHandleMultipleErrorsPerField()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Password", "The Password field is required.");
        modelState.AddModelError("Password", "The field Password must be a string with a minimum length of 8.");
        var context = CreateContext(modelState);

        _subject.OnActionExecuting(context);

        var result = context.Result as BadRequestObjectResult;
        var error = result!.Value as UksfErrorMessage;
        error!.Error.Should().Contain("required");
        error.Error.Should().Contain("minimum length");
    }

    [Fact]
    public void OnActionExecuting_ShouldSetNullValidation()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Code", "The Code field is required.");
        var context = CreateContext(modelState);

        _subject.OnActionExecuting(context);

        var result = context.Result as BadRequestObjectResult;
        var error = result!.Value as UksfErrorMessage;
        error!.Validation.Should().BeNull();
    }
}
