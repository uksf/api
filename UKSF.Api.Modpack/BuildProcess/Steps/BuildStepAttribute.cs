namespace UKSF.Api.Modpack.BuildProcess.Steps;

public class BuildStepAttribute : Attribute
{
    public readonly string Name;

    public BuildStepAttribute(string name)
    {
        Name = name;
    }
}
