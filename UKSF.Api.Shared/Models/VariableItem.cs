namespace UKSF.Api.Shared.Models;

public class VariableItem : MongoObject
{
    public object Item { get; set; }
    public string Key { get; set; }
}
