using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Moq.Protected;
using UKSF.Api.Core;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Modpack.Services;
using Xunit;

namespace UKSF.Api.Modpack.Tests.Services;

public class SteamApiServiceTests
{
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory = new();
    private readonly Mock<IUksfLogger> _mockLogger = new();
    private readonly SteamApiService _subject;

    public SteamApiServiceTests()
    {
        _subject = new SteamApiService(_mockHttpClientFactory.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task GetWorkshopModInfo_ShouldReturnModInfo_WhenValidResponse()
    {
        var json = """
                   {
                       "response": {
                           "publishedfiledetails": [{
                               "result": 1,
                               "title": "Test Mod",
                               "time_updated": 1700000000
                           }]
                       }
                   }
                   """;
        SetupHttpResponse(json, HttpStatusCode.OK);

        var result = await _subject.GetWorkshopModInfo("12345");

        result.Name.Should().Be("Test Mod");
        result.UpdatedDate.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1700000000).UtcDateTime);
    }

    [Fact]
    public async Task GetWorkshopModInfo_ShouldThrowBadRequest_WhenModNotFound()
    {
        var json = """
                   {
                       "response": {
                           "publishedfiledetails": [{
                               "result": 9
                           }]
                       }
                   }
                   """;
        SetupHttpResponse(json, HttpStatusCode.OK);

        var act = () => _subject.GetWorkshopModInfo("99999");

        await act.Should().ThrowAsync<BadRequestException>().WithMessage("*99999*not found*");
    }

    [Fact]
    public async Task GetWorkshopModInfo_ShouldThrow_WhenResponseMissingFields()
    {
        var json = """
                   {
                       "response": {
                           "publishedfiledetails": [{
                               "result": 1
                           }]
                       }
                   }
                   """;
        SetupHttpResponse(json, HttpStatusCode.OK);

        var act = () => _subject.GetWorkshopModInfo("12345");

        await act.Should().ThrowAsync<Exception>().WithMessage("*Failed getting info*");
    }

    [Fact]
    public async Task GetWorkshopModInfo_ShouldThrow_WhenEmptyResponse()
    {
        var json = """
                   {
                       "response": {
                           "publishedfiledetails": []
                       }
                   }
                   """;
        SetupHttpResponse(json, HttpStatusCode.OK);

        var act = () => _subject.GetWorkshopModInfo("12345");

        await act.Should().ThrowAsync<Exception>().WithMessage("*Failed getting info*");
    }

    [Fact]
    public async Task GetWorkshopModInfo_ShouldThrow_WhenHttpRequestFails()
    {
        SetupHttpResponse("", HttpStatusCode.InternalServerError);

        var act = () => _subject.GetWorkshopModInfo("12345");

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task GetWorkshopModInfo_ShouldLogAndRethrow_WhenJsonParsingFails()
    {
        SetupHttpResponse("not json at all", HttpStatusCode.OK);

        var act = () => _subject.GetWorkshopModInfo("12345");

        await act.Should().ThrowAsync<System.Text.Json.JsonException>();
        _mockLogger.Verify(x => x.LogError(It.Is<string>(s => s.Contains("Failed to parse JSON")), It.IsAny<Exception>()), Times.Once);
    }

    [Fact]
    public async Task GetWorkshopModInfo_ShouldReturnNoName_WhenTitleIsNull()
    {
        var json = """
                   {
                       "response": {
                           "publishedfiledetails": [{
                               "result": 1,
                               "title": null,
                               "time_updated": 1700000000
                           }]
                       }
                   }
                   """;
        SetupHttpResponse(json, HttpStatusCode.OK);

        var result = await _subject.GetWorkshopModInfo("12345");

        result.Name.Should().Be("NO NAME FOUND");
    }

    private void SetupHttpResponse(string content, HttpStatusCode statusCode)
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
                   .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                   .ReturnsAsync(new HttpResponseMessage { StatusCode = statusCode, Content = new StringContent(content) });

        var client = new HttpClient(mockHandler.Object);
        _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(client);
    }
}
