using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Moq.Protected;
using UKSF.Api.ArmaServer.Npc.Services;
using UKSF.Api.Core;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Npc;

public class GameServerCommandSenderTests
{
    [Fact]
    public async Task PostsSqfBodyToLocalhostCommandEndpoint()
    {
        HttpRequestMessage captured = null;
        string capturedBody = null;
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
               .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
               .Callback<HttpRequestMessage, CancellationToken>((req, _) =>
                   {
                       captured = req;
                       capturedBody = req.Content!.ReadAsStringAsync().Result;
                   }
               )
               .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(new HttpClient(handler.Object));

        var sender = new GameServerCommandSender(factory.Object, Mock.Of<IUksfLogger>());
        await sender.SendCommandAsync(5006, "[\"npc_audio\",\"n\",\"t\",0,1,\"QQ==\",100]");

        captured.RequestUri!.ToString().Should().Be("http://localhost:5006/command");
        capturedBody.Should().Be("[\"npc_audio\",\"n\",\"t\",0,1,\"QQ==\",100]");
    }
}
