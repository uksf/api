using System.Globalization;

namespace UKSF.Api.Modpack.Models;

public class ModpackBuildStep
{
    public ModpackBuildStep(string name)
    {
        Name = name;
    }

    public ModpackBuildResult BuildResult { get; set; } = ModpackBuildResult.None;
    public DateTime EndTime { get; set; } = DateTime.ParseExact("20000101", "yyyyMMdd", CultureInfo.InvariantCulture);
    public bool Finished { get; set; }
    public int Index { get; set; }
    public List<ModpackBuildStepLogItem> Logs { get; set; } = new();
    public string Name { get; set; }
    public bool Running { get; set; }
    public DateTime StartTime { get; set; } = DateTime.ParseExact("20000101", "yyyyMMdd", CultureInfo.InvariantCulture);
}
