using System.Collections.Generic;

namespace UKSF.Api.ArmaMissions.Models
{
    public class MissionEntity
    {
        public int ItemsCount;
        public List<MissionEntityItem> MissionEntityItems = new();
    }
}
