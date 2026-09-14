namespace UKSF.Api.Modpack.Models;

public class NewBuild
{
    public bool Ace { get; set; }
    public bool Acre { get; set; }
    public bool Air { get; set; }
    public string Reference { get; set; }
    public string Configuration { get; set; }

    /// <summary>Replaces the commit message shown as the build's changes, for builds whose content is not the commit itself.</summary>
    public string Changes { get; set; }
}
