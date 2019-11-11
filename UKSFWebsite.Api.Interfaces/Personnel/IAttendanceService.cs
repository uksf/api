using System;
using System.Threading.Tasks;
using UKSFWebsite.Api.Models.Personnel;

namespace UKSFWebsite.Api.Interfaces.Personnel {
    public interface IAttendanceService {
        Task<AttendanceReport> GenerateAttendanceReport(DateTime start, DateTime end);
    }
}
