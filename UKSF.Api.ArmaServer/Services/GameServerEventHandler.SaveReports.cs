namespace UKSF.Api.ArmaServer.Services;

public partial class GameServerEventHandler
{
    private void ReportUnstoredShutdownSave(int apiPort, Dictionary<string, object> data)
    {
        var outcome = data.GetValueOrDefault("saveOutcome")?.ToString();
        if (outcome is not null && outcome != "delivered")
        {
            logger.LogError($"Server on API port {apiPort} shut down without its save stored by the API: {outcome}");
        }
    }

    private void ReportUnsavedMissionEnd(Dictionary<string, object> data)
    {
        if (IsFalse(data.GetValueOrDefault("saved")))
        {
            logger.LogWarning($"Mission session {data.GetValueOrDefault("sessionId")} ended without a persistence save");
        }
    }

    private void ReportPartialSave(Dictionary<string, object> data)
    {
        if (IsTrue(data.GetValueOrDefault("partial")))
        {
            logger.LogWarning($"Persistence save for key {data.GetValueOrDefault("key")} is partial: the server hit its capture cap");
        }
    }

    private static bool IsTrue(object value) => value is true || string.Equals(value?.ToString(), "true", StringComparison.OrdinalIgnoreCase);

    private static bool IsFalse(object value) => value is false || string.Equals(value?.ToString(), "false", StringComparison.OrdinalIgnoreCase);
}
