using UKSF.Api.Core;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Modpack.BuildProcess;

public class BuildProcessHelperFactory(IVariablesService variablesService, IBuildProcessTracker processTracker) : IBuildProcessHelperFactory
{
    public BuildProcessHelper Create(
        IStepLogger stepLogger,
        IUksfLogger logger,
        CancellationTokenSource cancellationTokenSource,
        bool suppressOutput = false,
        bool raiseErrors = true,
        bool errorSilently = false,
        List<string> errorExclusions = null,
        string ignoreErrorGateClose = "",
        string ignoreErrorGateOpen = "",
        string buildId = null
    )
    {
        return new BuildProcessHelper(
            stepLogger,
            logger,
            cancellationTokenSource,
            variablesService,
            processTracker,
            suppressOutput,
            raiseErrors,
            errorSilently,
            errorExclusions,
            ignoreErrorGateClose,
            ignoreErrorGateOpen,
            buildId
        );
    }
}
