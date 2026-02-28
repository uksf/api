using UKSF.Api.ArmaMissions.Models.Sqm;
using UKSF.Api.Core.Models;

namespace UKSF.Api.ArmaMissions.Models;

public class MissionPatchContext
{
    public string PboPath { get; init; }
    public string FolderPath { get; set; }
    public string BackupPath { get; set; }
    public string ModsPath { get; init; }
    public int DefaultMaxCurators { get; init; }

    public int NextEntityId { get; set; }

    public SqmDocument Sqm { get; set; }
    public DescriptionDocument Description { get; set; }
    public int MaxCurators { get; set; } = 5;
    public PatchData PatchData { get; set; }

    public List<ValidationReport> Reports { get; } = [];
    public int PlayerCount { get; set; }
    public bool Aborted { get; set; }

    public string SqmPath => Path.Combine(FolderPath, "mission.sqm");
    public string DescriptionPath => Path.Combine(FolderPath, "description.ext");
    public string CbaSettingsPath => Path.Combine(FolderPath, "cba_settings.sqf");
}
