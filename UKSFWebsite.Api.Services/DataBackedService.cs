using UKSFWebsite.Api.Interfaces;

namespace UKSFWebsite.Api.Services {
    public class DataBackedService<T> : IDataBackedService<T> {
        private readonly T data;

        protected DataBackedService(T data) => this.data = data;

        public T Data() => data;
    }
}
