using UKSF.Api.Interfaces.Data;

namespace UKSF.Tests.Unit.Common {
    public interface IMockDataService : IDataService<MockDataModel, IMockDataService> { }
}
