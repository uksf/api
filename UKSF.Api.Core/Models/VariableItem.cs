namespace UKSF.Api.Core.Models;

public class VariableItem : MongoObject
{
    public object Item { get; set; }
    public string Key { get; set; }
}
