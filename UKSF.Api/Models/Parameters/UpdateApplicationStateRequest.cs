using UKSF.Api.Shared.Models;

namespace UKSF.Api.Models.Parameters;

public class UpdateApplicationStateRequest
{
    public ApplicationState UpdatedState { get; set; }
}
