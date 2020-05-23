using UKSF.Api.Interfaces;

namespace UKSF.Api.Services {
    public abstract class DataBackedService<T> : IDataBackedService<T> {
        protected DataBackedService(T data) => Data = data;

        public T Data { get; }
    }
}
