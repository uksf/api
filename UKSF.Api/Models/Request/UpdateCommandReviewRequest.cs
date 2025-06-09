using UKSF.Api.Core.Models.Domain;

namespace UKSF.Api.Models.Request;

public class UpdateCommandReviewRequest
{
    public ReviewState ReviewState { get; set; }
    public bool Overriden { get; set; }
}
