namespace UKSF.Api.Core.Models;

public class OprepDataset
{
    public IEnumerable<IGrouping<string, AccountAttendanceStatus>> GroupedAttendance { get; set; }
    public Oprep OperationEntity { get; set; }
}
