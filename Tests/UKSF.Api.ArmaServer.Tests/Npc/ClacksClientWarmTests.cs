using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Moq.Protected;
using UKSF.Api.ArmaServer.Npc.Services;
using UKSF.Api.Core;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Npc;

public class ClacksClientWarmTests
{
    private static (ClacksClient client, List<HttpRequestMessage> sent) Build(HttpStatusCode status, string responseJson = "{\"results\":[]}")
    {
        var sent = new List<HttpRequestMessage>();
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
               .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
               .Callback<HttpRequestMessage, CancellationToken>((req, _) => sent.Add(req))
               .ReturnsAsync(new HttpResponseMessage(status) { Content = new StringContent(responseJson, Encoding.UTF8, "application/json") });

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(new HttpClient(handler.Object));

        var variables = new Mock<IVariablesService>();
        variables.Setup(x => x.GetVariable("CLACKS_URL")).Returns(new DomainVariableItem { Key = "CLACKS_URL", Item = "http://dedi-ts:8800" });

        return (new ClacksClient(factory.Object, variables.Object, Mock.Of<IUksfLogger>()), sent);
    }

    [Fact]
    public async Task WarmAsync_PostsRolesAndLeaseToWarmEndpoint_AndReturnsTrue()
    {
        var (client, sent) = Build(HttpStatusCode.OK);

        var ok = await client.WarmAsync(["npc", "npc-voice"], 180_000);

        ok.Should().BeTrue();
        sent.Should().HaveCount(1);
        sent[0].Method.Should().Be(HttpMethod.Post);
        sent[0].RequestUri!.ToString().Should().Be("http://dedi-ts:8800/warm");
        var body = JsonDocument.Parse(await sent[0].Content!.ReadAsStringAsync());
        body.RootElement.GetProperty("leaseMs").GetInt32().Should().Be(180_000);
        var roles = body.RootElement.GetProperty("roles");
        roles.GetArrayLength().Should().Be(2);
        roles[0].GetString().Should().Be("npc");
        roles[1].GetString().Should().Be("npc-voice");
    }

    [Fact]
    public async Task WarmAsync_ReturnsFalseOnHttpFailure()
    {
        var (client, _) = Build(HttpStatusCode.ServiceUnavailable, "{\"error\":\"no route\"}");
        (await client.WarmAsync(["npc"], 60_000)).Should().BeFalse();
    }

    [Fact]
    public async Task WarmAsync_ReturnsFalseWhenClacksUrlNotConfigured()
    {
        var factory = new Mock<IHttpClientFactory>();
        var variables = new Mock<IVariablesService>();
        variables.Setup(x => x.GetVariable("CLACKS_URL")).Returns(new DomainVariableItem { Key = "CLACKS_URL", Item = null });
        var client = new ClacksClient(factory.Object, variables.Object, Mock.Of<IUksfLogger>());

        (await client.WarmAsync(["npc"], 60_000)).Should().BeFalse();
        factory.Verify(x => x.CreateClient(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task WarmAsync_ReturnsFalseWhenRolesEmpty_AndDoesNotCallHttp()
    {
        var (client, sent) = Build(HttpStatusCode.OK);
        (await client.WarmAsync([], 60_000)).Should().BeFalse();
        sent.Should().BeEmpty();
    }
}
