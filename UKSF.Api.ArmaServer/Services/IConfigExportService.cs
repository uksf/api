using UKSF.Api.ArmaServer.Models;

namespace UKSF.Api.ArmaServer.Services;

public interface IConfigExportService
{
    TriggerResult Trigger(string modpackVersion);
    ConfigExportStatusResponse GetStatus();
}

public record ConfigExportStatusResponse(string RunId, ConfigExportStatus Status, DateTime? StartedAt);
