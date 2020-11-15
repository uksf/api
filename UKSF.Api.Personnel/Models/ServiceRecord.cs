using System;

namespace UKSF.Api.Personnel.Models {
    public class ServiceRecordEntry : IEquatable<ServiceRecordEntry> {
        public string Notes { get; init; }
        public string Occurence { get; init; }
        public DateTime Timestamp { get; init; }

        public bool Equals(ServiceRecordEntry other) {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Notes == other.Notes && Occurence == other.Occurence && Timestamp.Equals(other.Timestamp);
        }

        public override string ToString() => $"{Timestamp:dd/MM/yyyy}: {Occurence}{(string.IsNullOrEmpty(Notes) ? "" : $"({Notes})")}";

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == GetType() && Equals((ServiceRecordEntry) obj);
        }

        public override int GetHashCode() => HashCode.Combine(Notes, Occurence, Timestamp);
    }
}
