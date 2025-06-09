namespace UKSF.Api.Models.Request;

public class UpdateDocumentContentRequest
{
    public string NewText { get; set; }
    public DateTime LastKnownUpdated { get; set; }
}
