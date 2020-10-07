using Moq;
using UKSF.Api.Events;
using UKSF.Api.Interfaces.Events.Handlers;
using Xunit;

namespace UKSF.Tests.Unit.Events {
    public class EventHandlerInitialiserTests {
        [Fact]
        public void ShouldInitEventHandlers() {
            Mock<IAccountEventHandler> mockAccountEventHandler = new Mock<IAccountEventHandler>();
            Mock<IBuildsEventHandler> mockBuildsEventHandler = new Mock<IBuildsEventHandler>();
            Mock<ICommandRequestEventHandler> mockCommandRequestEventHandler = new Mock<ICommandRequestEventHandler>();
            Mock<ICommentThreadEventHandler> mockCommentThreadEventHandler = new Mock<ICommentThreadEventHandler>();
            Mock<ILogEventHandler> mockLogEventHandler = new Mock<ILogEventHandler>();
            Mock<INotificationsEventHandler> mockNotificationsEventHandler = new Mock<INotificationsEventHandler>();
            Mock<ITeamspeakEventHandler> mockTeamspeakEventHandler = new Mock<ITeamspeakEventHandler>();

            mockAccountEventHandler.Setup(x => x.Init());
            mockCommandRequestEventHandler.Setup(x => x.Init());
            mockCommentThreadEventHandler.Setup(x => x.Init());
            mockLogEventHandler.Setup(x => x.Init());
            mockNotificationsEventHandler.Setup(x => x.Init());
            mockTeamspeakEventHandler.Setup(x => x.Init());

            EventHandlerInitialiser eventHandlerInitialiser = new EventHandlerInitialiser(
                mockAccountEventHandler.Object,
                mockBuildsEventHandler.Object,
                mockCommandRequestEventHandler.Object,
                mockCommentThreadEventHandler.Object,
                mockLogEventHandler.Object,
                mockNotificationsEventHandler.Object,
                mockTeamspeakEventHandler.Object
            );

            eventHandlerInitialiser.InitEventHandlers();

            mockAccountEventHandler.Verify(x => x.Init(), Times.Once);
            mockBuildsEventHandler.Verify(x => x.Init(), Times.Once);
            mockCommandRequestEventHandler.Verify(x => x.Init(), Times.Once);
            mockCommentThreadEventHandler.Verify(x => x.Init(), Times.Once);
            mockLogEventHandler.Verify(x => x.Init(), Times.Once);
            mockNotificationsEventHandler.Verify(x => x.Init(), Times.Once);
            mockTeamspeakEventHandler.Verify(x => x.Init(), Times.Once);
        }
    }
}
