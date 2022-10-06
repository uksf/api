using System.Collections.Generic;

namespace UKSF.Api.Tests.Common;

public class TestComplexDataModel : TestDataModel
{
    public TestDataModel Data { get; set; }
    public List<TestDataModel> DataList { get; set; }
    public List<string> List { get; set; }
}
