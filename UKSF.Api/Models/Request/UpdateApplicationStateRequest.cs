using UKSF.Api.Core.Models;

namespace UKSF.Api.Models.Request;

public class UpdateApplicationStateRequest
{
    public ApplicationState UpdatedState { get; set; }
}
