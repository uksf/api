using System.Collections.Generic;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.Tests.Common;

public class TestDataModel : MongoObject
{
    public Dictionary<string, object> Dictionary { get; set; } = new();
    public string Name { get; set; }
    public List<object> Stuff { get; set; }
}
