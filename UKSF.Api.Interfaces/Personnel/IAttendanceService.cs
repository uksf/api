using System;
using System.Threading.Tasks;
using UKSF.Api.Models.Personnel;

namespace UKSF.Api.Interfaces.Personnel {
    public interface IAttendanceService {
        Task<AttendanceReport> GenerateAttendanceReport(DateTime start, DateTime end);
    }
}
