using UKSF.Api.ArmaServer.Models;

namespace UKSF.Api.ArmaServer.Services;

public interface ISyntheticServerLauncher
{
    SyntheticLaunchResult Launch(SyntheticLaunchSpec spec);
}
