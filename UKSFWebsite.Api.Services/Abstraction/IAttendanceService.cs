using System;
using System.Threading.Tasks;
using UKSFWebsite.Api.Models;

namespace UKSFWebsite.Api.Services.Abstraction {
    public interface IAttendanceService {
        Task<AttendanceReport> GenerateAttendanceReport(DateTime start, DateTime end);
    }
}
