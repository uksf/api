using UKSF.Api.ArmaMissions.Models;
using UKSF.Api.ArmaMissions.Models.Sqm;
using UKSF.Api.Core;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models;

namespace UKSF.Api.ArmaMissions.Services;

public interface IMissionPatchingService
{
    Task<MissionPatchingResult> PatchMission(string path, string armaServerModsPath, int armaServerDefaultMaxCurators);
}

public class MissionPatchingService(
    IPboHandler pboHandler,
    ISqmReader sqmReader,
    ISqmWriter sqmWriter,
    ISqmPatcher sqmPatcher,
    IDescriptionReader descriptionReader,
    IDescriptionWriter descriptionWriter,
    IDescriptionPatcher descriptionPatcher,
    ISettingsReader settingsReader,
    IPatchDataBuilder patchDataBuilder,
    ISqmDecompiler sqmDecompiler,
    IHeadlessClientPatcher headlessClientPatcher,
    IDebugConsoleStripper debugConsoleStripper,
    IUksfLogger logger
) : IMissionPatchingService
{
    public async Task<MissionPatchingResult> PatchMission(string path, string armaServerModsPath, int armaServerDefaultMaxCurators)
    {
        var context = new MissionPatchContext
        {
            PboPath = path.Replace("\"", ""),
            ModsPath = armaServerModsPath,
            DefaultMaxCurators = armaServerDefaultMaxCurators
        };

        try
        {
            pboHandler.Backup(context);
            await pboHandler.Extract(context);

            descriptionReader.Read(context);
            if (context.Aborted)
            {
                return BuildResult(context);
            }

            settingsReader.Read(context);
            if (context.Aborted)
            {
                return BuildResult(context);
            }

            pboHandler.DeleteReadme(context);

            if (await sqmDecompiler.IsBinarized(context.SqmPath))
            {
                await sqmDecompiler.Decompile(context.SqmPath);
            }

            headlessClientPatcher.Patch(context);
            debugConsoleStripper.Patch(context);

            sqmReader.Read(context);

            if (context.Description.MissionPatchingIgnore)
            {
                context.Reports.Add(
                    new ValidationReport(
                        "Mission Patching Ignored",
                        "Mission patching for this mission was ignored.\nThis means no changes to the mission.sqm were made." +
                        "This is not an error, however errors may occur in the mission as a result of this.\n" +
                        "Ensure ALL the steps below have been done to the mission.sqm before reporting any errors:\n\n\n" +
                        "1: Remove raw newline characters. Any newline characters (\\n) in code will result in compile errors and that code will NOT run.\n" +
                        "For example, a line: init = \"myTestVariable = 10; \\n myOtherTestVariable = 20;\" should be replaced with: init = \"myTestVariable = 10; myOtherTestVariable = 20;\"\n\n" +
                        "2: Replace embedded quotes. Any embedded (double) quotes (\"\"hello\"\") in code will result in compile errors and that code will NOT run. They should be replaced with a single quote character (').\n" +
                        "For example, a line: init = \"myTestVariable = \"\"hello\"\";\" should be replaced with: init = \"myTestVariable = 'hello';\""
                    )
                );
                CountPlayable(context);
                descriptionPatcher.Patch(context);
                descriptionWriter.Write(context);
            }
            else
            {
                patchDataBuilder.Build(context);
                sqmPatcher.Patch(context);
                CountPlayable(context);
                sqmWriter.Write(context);
                pboHandler.CopyImage(context);
                descriptionPatcher.Patch(context);
                descriptionWriter.Write(context);
            }

            if (context.Reports.Any(r => r.Error))
            {
                return BuildResult(context);
            }

            if (context.Description.UseSimplePack)
            {
                logger.LogAudit($"Mission processed with simple packing enabled ({Path.GetFileName(path)})");
            }

            await pboHandler.Pack(context);
        }
        catch (Exception exception)
        {
            logger.LogError($"Mission patching failed ({exception.GetType().Name})", exception);
            return new MissionPatchingResult { Reports = [new ValidationReport(exception)], Success = false };
        }
        finally
        {
            pboHandler.Cleanup(context);
        }

        return BuildResult(context);
    }

    private static MissionPatchingResult BuildResult(MissionPatchContext context)
    {
        return new MissionPatchingResult
        {
            Reports = context.Reports,
            PlayerCount = context.PlayerCount,
            Success = context.Reports.All(r => !r.Error)
        };
    }

    private static void CountPlayable(MissionPatchContext context)
    {
        var count = 0;
        foreach (var entity in context.Sqm.Entities)
        {
            count += CountPlayableInEntity(entity);
        }

        context.PlayerCount = count;
    }

    private static int CountPlayableInEntity(SqmEntity entity)
    {
        switch (entity)
        {
            case SqmObject obj:  return obj.IsPlayable ? 1 : 0;
            case SqmGroup group: return group.Children.Sum(CountPlayableInEntity);
            default:             return 0;
        }
    }
}
