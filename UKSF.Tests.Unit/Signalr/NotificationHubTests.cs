using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Primitives;
using MongoDB.Bson;
using Moq;
using UKSF.Api.Signalr.Hubs.Message;
using Xunit;

namespace UKSF.Tests.Unit.Signalr {
    public class NotificationHubTests {
        public NotificationHubTests() {
            id = ObjectId.GenerateNewId().ToString();
            IQueryCollection queryCollection = new QueryCollection(new Dictionary<string, StringValues> {{"userId", id}});

            Mock<HttpContext> mockHttpContext = new Mock<HttpContext>();
            Mock<IHttpContextFeature> mockHttpContextFeature = new Mock<IHttpContextFeature>();
            mockHubCallerContext = new Mock<HubCallerContext>();
            mockGroupManager = new Mock<IGroupManager>();

            mockHttpContext.Setup(x => x.Request.Query).Returns(queryCollection);
            mockHttpContextFeature.Setup(x => x.HttpContext).Returns(mockHttpContext.Object);
            mockHubCallerContext.Setup(x => x.ConnectionId).Returns("1");
            mockHubCallerContext.Setup(x => x.Features.Get<IHttpContextFeature>()).Returns(mockHttpContextFeature.Object);
        }

        private readonly string id;
        private readonly Mock<HubCallerContext> mockHubCallerContext;
        private readonly Mock<IGroupManager> mockGroupManager;

        [Fact]
        public async Task ShouldAddToGroup() {
            Dictionary<string, string> subject = new Dictionary<string, string>();

            mockGroupManager.Setup(x => x.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), default)).Callback((string connectionId, string userId, CancellationToken _) => { subject.Add(connectionId, userId); });

            NotificationHub notificationHub = new NotificationHub {Context = mockHubCallerContext.Object, Groups = mockGroupManager.Object};
            await notificationHub.OnConnectedAsync();

            subject.Should().HaveCount(1);
            subject.Should().ContainKey("1");
            subject.Should().ContainValue(id);
        }

        [Fact]
        public async Task ShouldRemoveFromGroup() {
            Dictionary<string, string> subject = new Dictionary<string, string> {{"1", id}};

            mockGroupManager.Setup(x => x.RemoveFromGroupAsync(It.IsAny<string>(), It.IsAny<string>(), default)).Callback((string connectionId, string userId, CancellationToken _) => { subject.Remove(connectionId); });

            NotificationHub notificationHub = new NotificationHub {Context = mockHubCallerContext.Object, Groups = mockGroupManager.Object};
            await notificationHub.OnDisconnectedAsync(null);

            subject.Should().HaveCount(0);
        }
    }
}
