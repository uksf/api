using System.Collections.Generic;
using UKSF.Api.Base.Models;

namespace UKSF.Api.Tests.Common
{
    public class TestDataModel : MongoObject
    {
        public Dictionary<string, object> Dictionary = new();
        public string Name;
        public List<object> Stuff;
    }
}
