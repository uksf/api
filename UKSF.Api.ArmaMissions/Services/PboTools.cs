namespace UKSF.Api.ArmaMissions.Services;

public class PboTools : IPboTools
{
    private static readonly string[] MissionExcludePatterns = ["thumbs.db", "*.txt", "*.h", "*.dep", "*.cpp", "*.bak", "*.png", "*.log", "*.pew"];

    public Task ExtractPbo(string pboPath, string parentFolder)
    {
        var folderPath = Path.Combine(parentFolder, Path.GetFileNameWithoutExtension(pboPath));
        if (Directory.Exists(folderPath))
        {
            Directory.Delete(folderPath, true);
        }

        PboReader.Extract(pboPath, folderPath);
        return Task.CompletedTask;
    }

    public Task MakePbo(string folderPath, string pboPath, string workingDirectory)
    {
        PboWriter.Pack(folderPath, pboPath, MissionExcludePatterns);
        return Task.CompletedTask;
    }

    public Task SimplePackPbo(string folderPath, string pboPath, string workingDirectory)
    {
        PboWriter.Pack(folderPath, pboPath, []);
        return Task.CompletedTask;
    }
}
