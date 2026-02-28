namespace UKSF.Api.ArmaMissions.Services;

public interface IPboTools
{
    Task ExtractPbo(string pboPath, string parentFolder);
    Task MakePbo(string folderPath, string pboPath, string workingDirectory);
    Task SimplePackPbo(string folderPath, string pboPath, string workingDirectory);
}
