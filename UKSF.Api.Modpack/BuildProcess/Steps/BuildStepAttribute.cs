namespace UKSF.Api.Modpack.BuildProcess.Steps;

public class BuildStepAttribute(string name) : Attribute
{
    public readonly string Name = name;
}
