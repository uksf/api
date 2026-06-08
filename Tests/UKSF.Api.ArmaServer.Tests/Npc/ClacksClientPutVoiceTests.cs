using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.ArmaServer.Npc.Services;
using UKSF.Api.Core;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Npc;

public class ClacksClientPutVoiceTests
{
    private sealed class CapturingHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public HttpRequestMessage Captured;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Captured = request;
            // Buffer the content so it survives disposal of the request body.
            if (request.Content is not null) await request.Content.LoadIntoBufferAsync();
            return new HttpResponseMessage(status);
        }
    }

    private static (ClacksClient sut, CapturingHandler handler) Build(HttpStatusCode status)
    {
        var handler = new CapturingHandler(status);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(new HttpClient(handler));
        var variables = new Mock<IVariablesService>();
        variables.Setup(x => x.GetVariable("CLACKS_URL")).Returns(new DomainVariableItem { Key = "CLACKS_URL", Item = "http://localhost:8800/" });
        var logger = new Mock<IUksfLogger>();
        return (new ClacksClient(factory.Object, variables.Object, logger.Object), handler);
    }

    [Fact]
    public async Task Puts_wav_bytes_to_voice_endpoint_and_returns_true()
    {
        var (sut, handler) = Build(HttpStatusCode.OK);
        var ok = await sut.PutVoiceAsync("smuggler", new byte[] { 1, 2, 3 });
        ok.Should().BeTrue();
        handler.Captured.Method.Should().Be(HttpMethod.Put);
        handler.Captured.RequestUri!.ToString().Should().Be("http://localhost:8800/voice/smuggler");
        handler.Captured.Content!.Headers.ContentType!.MediaType.Should().Be("audio/wav");
    }

    [Fact]
    public async Task Returns_false_on_non_success()
    {
        var (sut, _) = Build(HttpStatusCode.InternalServerError);
        (await sut.PutVoiceAsync("smuggler", new byte[] { 1 })).Should().BeFalse();
    }
}
