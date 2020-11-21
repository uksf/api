using System;

namespace UKSF.Api.ArmaMissions.Models {
    public class MissionPatchingReport {
        public string Detail;
        public bool Error;
        public string Title;

        public MissionPatchingReport(Exception exception) {
            Title = exception.GetBaseException().Message;
            Detail = exception.ToString();
            Error = true;
        }

        public MissionPatchingReport(string title, string detail, bool error = false) {
            Title = error ? $"Error: {title}" : $"Warning: {title}";
            Detail = detail;
            Error = error;
        }
    }
}
