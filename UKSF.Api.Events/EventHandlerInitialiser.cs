using UKSF.Api.Interfaces.Events.Handlers;

namespace UKSF.Api.Events {
    public class EventHandlerInitialiser {
        private readonly IAccountEventHandler accountEventHandler;
        private readonly ICommandRequestEventHandler commandRequestEventHandler;
        private readonly ICommentThreadEventHandler commentThreadEventHandler;
        private readonly ILogEventHandler logEventHandler;
        private readonly INotificationsEventHandler notificationsEventHandler;
        private readonly ITeamspeakEventHandler teamspeakEventHandler;

        public EventHandlerInitialiser(
            IAccountEventHandler accountEventHandler,
            ICommandRequestEventHandler commandRequestEventHandler,
            ICommentThreadEventHandler commentThreadEventHandler,
            ILogEventHandler logEventHandler,
            INotificationsEventHandler notificationsEventHandler,
            ITeamspeakEventHandler teamspeakEventHandler
        ) {
            this.accountEventHandler = accountEventHandler;
            this.commandRequestEventHandler = commandRequestEventHandler;
            this.commentThreadEventHandler = commentThreadEventHandler;
            this.logEventHandler = logEventHandler;
            this.notificationsEventHandler = notificationsEventHandler;
            this.teamspeakEventHandler = teamspeakEventHandler;
        }

        public void InitEventHandlers() {
            accountEventHandler.Init();
            commandRequestEventHandler.Init();
            commentThreadEventHandler.Init();
            logEventHandler.Init();
            notificationsEventHandler.Init();
            teamspeakEventHandler.Init();
        }
    }
}
