namespace UKSF.Api.Models.Request;

public class CreateMedicAttachmentRequest
{
    public string Recipient { get; set; }
    public string TroopId { get; set; } // empty/null => detach
    public string Reason { get; set; }
}
