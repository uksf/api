using System.Collections.Generic;

namespace UKSF.Api.ArmaMissions.Models
{
    public class MissionEntityItem
    {
        public static double Position = 10;
        public static double CuratorPosition = 0.5;
        public string DataType;
        public bool IsPlayable;
        public MissionEntity MissionEntity;
        public List<string> RawMissionEntities = new();
        public List<string> RawMissionEntityItem = new();
        public string Type;
    }
}
