using System.Collections.Generic;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.ArmaMissions.Models
{
    public class MissionPatchingResult
    {
        public int PlayerCount;
        public List<ValidationReport> Reports = new();
        public bool Success;
    }
}
