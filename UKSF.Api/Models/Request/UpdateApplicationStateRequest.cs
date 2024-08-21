using UKSF.Api.Core.Models.Domain;

namespace UKSF.Api.Models.Request;

public class UpdateApplicationStateRequest
{
    public ApplicationState UpdatedState { get; set; }
}
