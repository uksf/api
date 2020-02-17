namespace UKSF.Api.Interfaces {
    public interface IDataBackedService<out T> {
        T Data { get; }
    }
}
