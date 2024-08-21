using UKSF.Api.Core.Models.Domain;

namespace UKSF.Api.Core.Models;

public class OprepDataset
{
    public IEnumerable<IGrouping<string, AccountAttendanceStatus>> GroupedAttendance { get; set; }
    public DomainOprep OperationEntity { get; set; }
}
