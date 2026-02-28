using UKSF.Api.ArmaMissions.Models;

namespace UKSF.Api.ArmaMissions.Services;

public interface IDescriptionWriter
{
    void Write(MissionPatchContext context);
}

public class DescriptionWriter : IDescriptionWriter
{
    public void Write(MissionPatchContext context)
    {
        File.WriteAllLines(context.DescriptionPath, context.Description.Lines);
    }
}
