namespace UKSF.Api.Modpack.Models;

public class ModpackBuildStepLogItem
{
    public string Colour { get; set; }
    public string Text { get; set; }
}

public class ModpackBuildStepLogItemUpdate
{
    public bool Inline { get; set; }
    public List<ModpackBuildStepLogItem> Logs { get; set; }
}
