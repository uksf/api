namespace UKSFWebsite.Api.Interfaces {
    public interface IDataBackedService<out T> {
        T Data();
    }
}
