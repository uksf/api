using UKSF.Api.Core.Models;

namespace UKSF.Api.ArmaMissions.Models;

public class MissionPatchingResult
{
    public int PlayerCount { get; set; }
    public List<ValidationReport> Reports { get; set; } = new();
    public bool Success { get; set; }
}
