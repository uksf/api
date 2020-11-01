using System;

namespace UKSF.Api.Personnel.Models {
    public class ServiceRecordEntry : IEquatable<ServiceRecordEntry> {
        public string notes;
        public string occurence;
        public DateTime timestamp;

        public bool Equals(ServiceRecordEntry other) {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return notes == other.notes && occurence == other.occurence && timestamp.Equals(other.timestamp);
        }

        public override string ToString() => $"{timestamp:dd/MM/yyyy}: {occurence}{(string.IsNullOrEmpty(notes) ? "" : $"({notes})")}";

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == GetType() && Equals((ServiceRecordEntry) obj);
        }

        public override int GetHashCode() => HashCode.Combine(notes, occurence, timestamp);
    }
}
