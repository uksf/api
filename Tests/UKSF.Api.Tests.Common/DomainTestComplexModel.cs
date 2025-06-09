using System.Collections.Generic;

namespace UKSF.Api.Tests.Common;

public class DomainTestComplexModel : DomainTestModel
{
    public DomainTestModel Data { get; set; }
    public List<DomainTestModel> DataList { get; set; }
    public List<string> List { get; set; }
}
