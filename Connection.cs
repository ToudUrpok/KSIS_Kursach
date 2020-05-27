using System;
using System.Net.Sockets;

namespace ProxyServer
{
    public class Connection
    {
        public Socket ClientSocket;
        public Socket RedirectionSocket;

        public Connection(Socket clientSocket)
        {
            ClientSocket = clientSocket;
            RedirectionSocket = null;
        }

        public void CloseConnection()
        {
            ClientSocket?.Close();
            RedirectionSocket?.Close();
        }
    }
}
