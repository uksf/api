using System.Collections.Generic;
using System.Threading.Tasks;
using MongoDB.Driver;
using UKSFWebsite.Api.Models;
using UKSFWebsite.Api.Models.Requests;
using UKSFWebsite.Api.Services.Abstraction;

namespace UKSFWebsite.Api.Services.Data {
    public class OperationReportService : CachedDataService<Oprep>, IOperationReportService {
        private readonly IAttendanceService attendanceService;

        public OperationReportService(IMongoDatabase database, IAttendanceService attendanceService) : base(database, "oprep") => this.attendanceService = attendanceService;

        public async Task Create(CreateOperationReportRequest request) {
            Oprep operation = new Oprep {
                name = request.name,
                map = request.map,
                start = request.start.AddHours((double) request.starttime / 100),
                end = request.end.AddHours((double) request.endtime / 100),
                type = request.type,
                result = request.result
            };
            operation.attendanceReport = await attendanceService.GenerateAttendanceReport(operation.start, operation.end);
            await base.Add(operation);
        }

        public override List<Oprep> Get() {
            List<Oprep> reversed = base.Get();
            reversed.Reverse();
            return reversed;
        }

        public async Task Replace(Oprep request) {
            await Database.GetCollection<Oprep>(DatabaseCollection).ReplaceOneAsync(x => x.id == request.id, request);
        }
    }
}
