using System;

namespace UKSF.Api.Personnel.Models
{
    public class ServiceRecordEntry
    {
        public string Notes;
        public string Occurence;
        public DateTime Timestamp;

        public override string ToString()
        {
            return $"{Timestamp:dd/MM/yyyy}: {Occurence}{(string.IsNullOrEmpty(Notes) ? "" : $"({Notes})")}";
        }
    }
}
