using UKSF.Api.Shared.Context.Base;

namespace UKSF.Api.Tests.Common;

public interface ITestContext : IMongoContext<TestDataModel> { }
