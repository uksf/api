using System.Collections.Generic;
using System.Linq;
using UKSF.Api.Personnel.Models;

namespace UKSF.Api.Command.Models
{
    public class OprepDataset
    {
        public IEnumerable<IGrouping<string, AccountAttendanceStatus>> GroupedAttendance;
        public Oprep OperationEntity;
    }
}
