using UKSF.Api.Shared.Context;

namespace UKSF.Api.Tests.Common {
    public interface ITestContext : IMongoContext<TestDataModel> { }
}
