namespace UKSF.Api.Interfaces.Data {
    public interface IDataCollectionFactory {
        IDataCollection<T> CreateDataCollection<T>(string collectionName);
    }
}
