using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Moq.Protected;
using UKSF.Api.ArmaServer.Npc.Observability;
using UKSF.Api.ArmaServer.Npc.Services;
using UKSF.Api.Core;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Npc.Observability;

public class NpcTraceScopeTests
{
    private static (IHttpClientFactory factory, IVariablesService variables) Http(HttpStatusCode status, string response)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
               .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
               .ReturnsAsync(() => new HttpResponseMessage(status) { Content = new StringContent(response, Encoding.UTF8, "application/json") });
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler.Object));
        var variables = new Mock<IVariablesService>();
        variables.Setup(x => x.GetVariable("CLACKS_URL")).Returns(new DomainVariableItem { Key = "CLACKS_URL", Item = "http://clacks:8800" });
        return (factory.Object, variables.Object);
    }

    [Fact]
    public async Task ChatAsync_AddsExactRequestAndRawResponse_ToCurrentScope()
    {
        const string raw = "{\"model\":\"m\",\"choices\":[{\"message\":{\"content\":\"Get back.\"}}]}";
        var (factory, variables) = Http(HttpStatusCode.OK, raw);
        var client = new ClacksClient(factory, variables, Mock.Of<IUksfLogger>());

        using var _ = NpcTraceScope.Begin();
        await Task.Run(() => client.ChatAsync("npc", "SYS", "USR", json: true, maxTokens: 80, temperature: 0.7));

        var call = NpcTraceScope.Current.Take("chat").Should().ContainSingle().Subject;
        call.Request.Should().Contain("\"SYS\"").And.Contain("\"USR\"");
        call.Response.Should().Be(raw);
        call.Status.Should().Be("ok");
    }

    [Fact]
    public async Task JevAskAsync_RecordsFailedCallWithStatus()
    {
        var (factory, variables) = Http(HttpStatusCode.BadGateway, "upstream down");
        var client = new NpcJevClient(factory, variables, Mock.Of<IUksfLogger>());

        using var _ = NpcTraceScope.Begin();
        var result = await client.AskAsync("state", new() { ["to_me"] = JevQuestion.Noul("Is it for you?") }, "npc1");

        result.Should().BeNull();
        var call = NpcTraceScope.Current.Take("jev").Should().ContainSingle().Subject;
        call.Status.Should().Be("http 502");
        call.Response.Should().Be("upstream down");
        call.Request.Should().Contain("\"to_me\"");
    }

    [Fact]
    public async Task ModelCalls_OutsideAScope_AreIgnored()
    {
        var (factory, variables) = Http(HttpStatusCode.OK, "{\"choices\":[]}");
        var client = new ClacksClient(factory, variables, Mock.Of<IUksfLogger>());

        await client.ChatAsync("npc", "SYS", "USR", json: true, maxTokens: 80, temperature: 0.7);

        NpcTraceScope.Current.Should().BeNull();
    }
}
