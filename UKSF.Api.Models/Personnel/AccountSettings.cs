using System;
using System.Reflection;

namespace UKSF.Api.Models.Personnel {
    public class AccountSettings {
        public bool errorEmails = false;
        public bool notificationsEmail = true;
        public bool notificationsTeamspeak = true;
        public bool notificationsBuilds = false;
        public bool sr1Enabled = true;

        public T GetAttribute<T>(string name) {
            FieldInfo setting = typeof(AccountSettings).GetField(name);
            if (setting == null) throw new ArgumentException($"Could not find setting with name '{name}'");
            return (T) setting.GetValue(this);
        }
    }
}
