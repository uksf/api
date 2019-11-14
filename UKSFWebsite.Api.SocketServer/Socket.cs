using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UKSFWebsite.Api.Events.Data;
using UKSFWebsite.Api.Interfaces.Events;
using UKSFWebsite.Api.Interfaces.Integrations;
using UKSFWebsite.Api.Models.Events;
using UKSFWebsite.Api.Models.Integrations;
using Valve.Sockets;

namespace UKSFWebsite.Api.SocketServer {
    public class Socket : ISocket {
        private readonly Dictionary<uint, string> clients = new Dictionary<uint, string>();
        private readonly IEventBus<SocketEventModel> eventBus;
        private NetworkingSockets server;
        private uint socket;

        public Socket(ISocketEventBus eventBus) {
            this.eventBus = eventBus;
            Library.Initialize();
        }

        public void Start(string portString) {
            if (server != null) return;
            ushort port = ushort.Parse(portString);
            Address address = new Address();
            address.SetAddress("::0", port);
            server = new NetworkingSockets();
            socket = server.CreateListenSocket(ref address);

            server.DispatchCallback(StatusCallback);
            server.ReceiveMessagesOnListenSocket(socket, ReceiveMessage, 20);

            Console.WriteLine($"Socket server running on port {port}");
        }

        public void Stop() {
            Console.WriteLine("Socket server shutting down");
            server.CloseListenSocket(socket);
            server = null;
            Library.Deinitialize();
        }

        public void SendMessageToAllClients(string message) {
            Task.Run(() => SendMessageToAllClientsAsync(message));
        }

        public void SendMessageToClient(string clientName, string message) {
            Task.Run(() => SendMessageToClientAsync(clientName, message));
        }

        public void SendMessageToClient(string clientName, byte[] data) {
            Task.Run(() => SendMessageToClientAsync(clientName, data));
        }

        public bool IsClientOnline(string clientName) {
            uint client = GetClientByName(clientName);
            return client != default;
        }

        private void SendMessageToAllClientsAsync(string message) {
            foreach (string client in clients.Values) {
                SendMessageToClient(client, message);
            }
        }

        private void SendMessageToClientAsync(string clientName, string message) {
            byte[] data = Encoding.UTF8.GetBytes(message);
            uint client = GetClientByName(clientName);
            server.SendMessageToConnection(client, data);
        }

        private void SendMessageToClientAsync(string clientName, byte[] data) {
            uint client = GetClientByName(clientName);
            server.SendMessageToConnection(client, data);
        }

        private uint GetClientByName(string clientName) {
            return clients.FirstOrDefault(x => x.Value == clientName).Key;
        }

        private string GetClientName(uint client) {
            return clients.FirstOrDefault(x => x.Key == client).Value;
        }

        private void StatusCallback(StatusInfo info, IntPtr context) {
            switch (info.connectionInfo.state) {
                case ConnectionState.NONE: break;

                case ConnectionState.CONNECTING:
                    server.AcceptConnection(info.connection);
                    break;

                case ConnectionState.CONNECTED:
                    clients.Add(info.connection, GetClientName(info.connection));
                    Console.WriteLine($"Client connected - ID: {info.connection}, Name: {clients[info.connection]}");
                    break;

                case ConnectionState.CLOSED_BY_PEER:
                    clients.Remove(info.connection);
                    server.CloseConnection(info.connection);
                    Console.WriteLine($"Client disconnected - ID: {info.connection}, Name: {clients[info.connection]}");
                    break;
                case ConnectionState.FINDING_ROUTE: break;
                case ConnectionState.PROBLEM_DETECTED_LOCALLY: break;
                default: throw new ArgumentOutOfRangeException();
            }
        }

        private void ReceiveMessage(in NetworkingMessage message) {
            Console.WriteLine($"Message received from - ID: {message.connection}, Channel ID: {message.channel}, Data length: {message.length}");
            byte[] buffer = new byte[message.length];
            message.CopyTo(buffer);
            string messageString = Encoding.UTF8.GetString(buffer);
            if (ResolveMessage(message.connection, messageString)) return;
            eventBus.Send(EventModelFactory.CreateSocketEvent(GetClientName(message.connection), messageString));
        }

        private bool ResolveMessage(uint client, string messageString) {
            if (!Enum.TryParse(messageString.Substring(0, 1), out SocketCommands command)) return false;
            switch (command) {
                case SocketCommands.NAME:
                    if (!clients.ContainsKey(client)) return true;
                    string newName = messageString.Substring(1);
                    clients[client] = newName;
                    return true;
                default: return false;
            }
        }
    }
}
