using System.Net;
using System.Net.Sockets;

namespace ProxyServer
{
    public class Requests_Listener
    {
        private TcpListener RequestsListener;

        delegate void ConnectionRequestHandler(Socket socket);
        event ConnectionRequestHandler eConnectionRequestAccepted;

        public Requests_Listener(IPAddress address, int port)
        {
            RequestsListener = new TcpListener(address, port);
            eConnectionRequestAccepted += Server.CreateConnectionProcessingThread;
            Server.WriteMessage("Прокси-сервер готов к получению запросов на адрес {0} порт {1}.", address, port);
        }

        public void StartListen()
        {
            RequestsListener.Start();
            while (true)
            {
                if (RequestsListener.Pending())
                {
                    eConnectionRequestAccepted(RequestsListener.AcceptSocket());
                }
            }
        }       
    }
}
