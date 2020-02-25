using UKSF.Api.Interfaces.Data;

namespace UKSF.Tests.Unit.Common {
    public interface IMockCachedDataService : IDataService<MockDataModel, IMockCachedDataService> { }
}
