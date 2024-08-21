using System.Collections.Generic;
using UKSF.Api.Core.Models;

namespace UKSF.Api.Tests.Common;

public class DomainTestModel : MongoObject
{
    public Dictionary<string, object> Dictionary { get; set; } = new();
    public string Name { get; set; }
    public List<object> Stuff { get; set; }
}
