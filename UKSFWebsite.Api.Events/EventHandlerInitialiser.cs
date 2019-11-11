using UKSFWebsite.Api.Interfaces.Events;

namespace UKSFWebsite.Api.Events {
    public class EventHandlerInitialiser {
        private readonly IAccountEventHandler accountEventHandler;
        private readonly ICommandRequestEventHandler commandRequestEventHandler;
        private readonly ICommentThreadEventHandler commentThreadEventHandler;
        private readonly INotificationsEventHandler notificationsEventHandler;

        public EventHandlerInitialiser(IAccountEventHandler accountEventHandler, ICommandRequestEventHandler commandRequestEventHandler, ICommentThreadEventHandler commentThreadEventHandler, INotificationsEventHandler notificationsEventHandler) {
            this.accountEventHandler = accountEventHandler;
            this.commandRequestEventHandler = commandRequestEventHandler;
            this.commentThreadEventHandler = commentThreadEventHandler;
            this.notificationsEventHandler = notificationsEventHandler;
        }

        public void InitEventHandlers() {
            accountEventHandler.Init();
            commandRequestEventHandler.Init();
            commentThreadEventHandler.Init();
            notificationsEventHandler.Init();
        }
    }
}
