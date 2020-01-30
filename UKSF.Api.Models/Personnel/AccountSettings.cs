namespace UKSF.Api.Models.Personnel {
    public class AccountSettings {
        public bool errorEmails = false;
        public bool notificationsEmail = true;
        public bool notificationsTeamspeak = true;
        public bool sr1Enabled = true;

        public T GetAttribute<T>(string name) => (T) typeof(AccountSettings).GetField(name).GetValue(this);
    }
}
