using UKSF.Api.Base.Models;

namespace UKSF.Api.Personnel.Models;

public class ConfirmationCode : MongoObject
{
    public string Value { get; set; }
}
