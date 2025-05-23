using UKSF.Api.Core;

namespace UKSF.Api.Modpack.BuildProcess;

public interface IBuildProcessHelperFactory
{
    BuildProcessHelper Create(
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
    );
}
