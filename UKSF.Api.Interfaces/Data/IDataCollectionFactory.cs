namespace UKSF.Api.Interfaces.Data {
    public interface IDataCollectionFactory {
        IDataCollection CreateDataCollection(string collectionName);
    }
}
