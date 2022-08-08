using UKSF.Api.Personnel.Models;

namespace UKSF.Api.Command.Models;

public class OprepDataset
{
    public IEnumerable<IGrouping<string, AccountAttendanceStatus>> GroupedAttendance { get; set; }
    public Oprep OperationEntity { get; set; }
}
