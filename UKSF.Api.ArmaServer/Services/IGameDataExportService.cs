using UKSF.Api.ArmaServer.Models;

namespace UKSF.Api.ArmaServer.Services;

public interface IGameDataExportService
{
    TriggerResult Trigger(string modpackVersion);
    GameDataExportStatusResponse GetStatus();
}

public record GameDataExportStatusResponse(string RunId, GameDataExportStatus Status, DateTime? StartedAt);
