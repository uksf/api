using UKSF.Api.ArmaMissions.Models;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ArmaMissions.Services;

public interface IPboHandler
{
    void Backup(MissionPatchContext context);
    Task Extract(MissionPatchContext context);
    Task Pack(MissionPatchContext context);
    void Cleanup(MissionPatchContext context);
    void DeleteReadme(MissionPatchContext context);
    void CopyImage(MissionPatchContext context);
}

public class PboHandler(IPboTools pboTools, IVariablesService variablesService) : IPboHandler
{
    public void Backup(MissionPatchContext context)
    {
        var backupPath = Path.Combine(
            variablesService.GetVariable("MISSIONS_BACKUPS").AsString(),
            Path.GetFileName(context.PboPath) ?? throw new FileNotFoundException()
        );

        Directory.CreateDirectory(Path.GetDirectoryName(backupPath) ?? throw new DirectoryNotFoundException());
        File.Copy(context.PboPath, backupPath, true);
        if (!File.Exists(backupPath))
        {
            throw new FileNotFoundException("Could not create backup", backupPath);
        }

        context.BackupPath = backupPath;
    }

    public async Task Extract(MissionPatchContext context)
    {
        if (Path.GetExtension(context.PboPath) != ".pbo")
        {
            throw new FileLoadException("File is not a pbo");
        }

        var parentFolderPath = Path.GetDirectoryName(context.PboPath);
        context.FolderPath = Path.Combine(parentFolderPath!, Path.GetFileNameWithoutExtension(context.PboPath));
        await pboTools.ExtractPbo(context.PboPath, parentFolderPath!);
    }

    public async Task Pack(MissionPatchContext context)
    {
        var filePath = context.PboPath;
        if (Directory.Exists(filePath))
        {
            filePath += ".pbo";
        }

        var workingDir = variablesService.GetVariable("MISSIONS_WORKING_DIR").AsString();
        if (context.Description.UseSimplePack)
        {
            await pboTools.SimplePackPbo(context.FolderPath, filePath, workingDir);
        }
        else
        {
            await pboTools.MakePbo(context.FolderPath, filePath, workingDir);
        }
    }

    public void Cleanup(MissionPatchContext context)
    {
        try
        {
            if (context.FolderPath is not null)
            {
                Directory.Delete(context.FolderPath, true);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Temp directory cleanup is best-effort
        }
    }

    public void DeleteReadme(MissionPatchContext context)
    {
        var readmePath = Path.Combine(context.FolderPath, "README.txt");
        if (File.Exists(readmePath))
        {
            File.Delete(readmePath);
        }
    }

    public void CopyImage(MissionPatchContext context)
    {
        if (context.Description.Lines.Any(x => x.ContainsIgnoreCase("missionImageIgnore")))
        {
            return;
        }

        var imagePath = Path.Combine(context.FolderPath, "uksf.paa");
        var modpackImagePath = Path.Combine(context.ModsPath, "@uksf", "UKSFTemplate.VR", "uksf.paa");
        if (!File.Exists(modpackImagePath))
        {
            return;
        }

        if (File.Exists(imagePath) && new FileInfo(imagePath).Length != new FileInfo(modpackImagePath).Length)
        {
            context.Reports.Add(
                new ValidationReport(
                    "Loading image was different",
                    "The mission loading image `uksf.paa` was found to be different from the default." +
                    "It has been replaced with the default UKSF image.\n\n" +
                    "If you wish this to be a custom image, see <a target=\"_blank\" href=https://github.com/uksf/modpack/wiki/SR5:-Mission-Patching#ignoring-custom-loading-image>this page</a> for details on how to configure this"
                )
            );
        }

        File.Copy(modpackImagePath, imagePath, true);
    }
}
