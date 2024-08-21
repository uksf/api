namespace UKSF.Api.Core.Models.Domain;

public class DomainVariableItem : MongoObject
{
    public object Item { get; set; }
    public string Key { get; set; }
}
