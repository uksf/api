using UKSF.Api.Shared.Models;

namespace UKSF.Api.Models.Parameters;

public class UpdateCommandReviewRequest
{
    public ReviewState ReviewState { get; set; }
    public bool Overriden { get; set; }
}
