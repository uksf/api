namespace UKSF.Api.Shared.Models;

public class OprepDataset
{
    public IEnumerable<IGrouping<string, AccountAttendanceStatus>> GroupedAttendance { get; set; }
    public Oprep OperationEntity { get; set; }
}
