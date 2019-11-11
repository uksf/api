using System;

namespace UKSFWebsite.Api.Models.Mission {
    public class MissionPatchingReport {
        public string detail;
        public bool error;
        public string title;

        public MissionPatchingReport(Exception exception) {
            title = exception.GetBaseException().Message;
            detail = exception.ToString();
            error = true;
        }

        public MissionPatchingReport(string title, string detail, bool error = false) {
            this.title = error ? $"Error: {title}" : $"Warning: {title}";
            this.detail = detail;
            this.error = error;
        }
    }
}
