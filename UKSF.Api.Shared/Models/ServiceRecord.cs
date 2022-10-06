namespace UKSF.Api.Shared.Models;

public class ServiceRecordEntry
{
    public string Notes { get; set; }
    public string Occurence { get; set; }
    public DateTime Timestamp { get; set; }

    public override string ToString()
    {
        return $"{Timestamp:dd/MM/yyyy}: {Occurence}{(string.IsNullOrEmpty(Notes) ? "" : $"({Notes})")}";
    }
}
