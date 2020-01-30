namespace UKSF.Api.Interfaces.Integrations {
    public interface ISocket {
        void Start(string port);
        void Stop();
        void SendMessageToAllClients(string message);
        void SendMessageToClient(string clientName, string message);
        void SendMessageToClient(string clientName, byte[] data);
        bool IsClientOnline(string clientName);
    }
}
