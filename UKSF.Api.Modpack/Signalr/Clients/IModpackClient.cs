using UKSF.Api.Modpack.Models;

namespace UKSF.Api.Modpack.Signalr.Clients;

public interface IModpackClient
{
    Task ReceiveReleaseCandidateBuild(DomainModpackBuild build);
    Task ReceiveBuild(DomainModpackBuild build);
    Task ReceiveBuildStep(ModpackBuildStep step);
    Task ReceiveRelease(DomainModpackRelease release);
    Task ReceiveWorkshopModUpdate(DomainWorkshopMod workshopMod);
}
