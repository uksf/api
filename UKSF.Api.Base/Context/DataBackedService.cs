namespace UKSF.Api.Base.Context {
    public interface IDataBackedService<out T> {
        T Data { get; }
    }

    // TODO: Either remove this and just make the services rely on the data service properly,
    // or make this protected and make data consumers use the data services directly
    public abstract class DataBackedService<T> : IDataBackedService<T> {
        protected DataBackedService(T data) => Data = data;

        public T Data { get; }
    }
}
