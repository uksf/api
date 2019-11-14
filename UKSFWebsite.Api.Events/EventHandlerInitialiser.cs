using UKSFWebsite.Api.Interfaces.Events.Handlers;

namespace UKSFWebsite.Api.Events {
    public class EventHandlerInitialiser {
        private readonly IAccountEventHandler accountEventHandler;
        private readonly ICommandRequestEventHandler commandRequestEventHandler;
        private readonly ICommentThreadEventHandler commentThreadEventHandler;
        private readonly INotificationsEventHandler notificationsEventHandler;
        private readonly ITeamspeakEventHandler teamspeakEventHandler;

        public EventHandlerInitialiser(IAccountEventHandler accountEventHandler, ICommandRequestEventHandler commandRequestEventHandler, ICommentThreadEventHandler commentThreadEventHandler, INotificationsEventHandler notificationsEventHandler, ITeamspeakEventHandler teamspeakEventHandler) {
            this.accountEventHandler = accountEventHandler;
            this.commandRequestEventHandler = commandRequestEventHandler;
            this.commentThreadEventHandler = commentThreadEventHandler;
            this.notificationsEventHandler = notificationsEventHandler;
            this.teamspeakEventHandler = teamspeakEventHandler;
        }

        public void InitEventHandlers() {
            accountEventHandler.Init();
            commandRequestEventHandler.Init();
            commentThreadEventHandler.Init();
            notificationsEventHandler.Init();
            teamspeakEventHandler.Init();
        }
    }
}
