using System;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.IO;

namespace ProxyServer
{
    public static class Server
    {
        static public bool NEED_AUTHORIZATION = false;
        static private string PASSWORD = "123456";
        static private string LOGIN = "client";

        static public bool ALLOW_FORBIDDEN_DOMAINS = true;
        static private string[] ForbiddenDomains = null;

        static public bool APPEND_HTML_PAGES = true;

        static private int LISTENING_PORT = 8888;
        static private Host_Network_Information ServerNetworkInformation = new Host_Network_Information();

        static private int RESPONSE_TIMEOUT = 1000000;

        static private Requests_Listener RequestsListener = new Requests_Listener(ServerNetworkInformation.HostIPAddress, LISTENING_PORT);

        static public HTTP_Parser HTTPParser = new HTTP_Parser();

        static public Messages_Handler MessagesHandler = new Messages_Handler();

        delegate void MessageHandler(Socket socket);
        static event MessageHandler eRequestReceived;

        static public void StartWork()
        {
            LoadForbiddenDomains();
            eRequestReceived += ReadAndHandleRequest;
            RequestsListener.StartListen();   
        }

        static private void LoadForbiddenDomains()
        {
            if (ALLOW_FORBIDDEN_DOMAINS)
            {
                if (File.Exists("ForbiddenDomains.txt"))
                {
                    ForbiddenDomains = File.ReadAllLines("ForbiddenDomains.txt");
                }
                else
                {
                    ALLOW_FORBIDDEN_DOMAINS = false;
                    WriteMessage("Файл 'ForbiddenDomains.txt' с запрещенными доменами не найден");
                }
            }
        }

        static public bool VarifyForbiddenDomain(string domain)
        {
            return (Array.IndexOf(ForbiddenDomains, domain) != -1);
        }

        static public bool VarifyAuthorizationData(string login, string password)
        {
            return (LOGIN == login) && (PASSWORD == password);
        }

        static public void CreateConnectionProcessingThread(Socket clientSocket)
        {
            Thread processNewConnection = new Thread(new ParameterizedThreadStart(ReceiveRequest));
            processNewConnection.IsBackground = true;
            processNewConnection.Start(clientSocket);
            //ReceiveRequest(clientSocket);
        }

        static private void ReceiveRequest(object socket)
        {
            using (var clientSocket = (Socket)socket)
            {
                while (clientSocket.Connected)
                {
                    if (clientSocket.Available != 0)
                        eRequestReceived(clientSocket);
                }
            }
        }

        static private void ReadAndHandleRequest(Socket clientSocket)
        {
            byte[] clientRequest;
            if (ReadToEnd(clientSocket, out clientRequest))
            {
                MessagesHandler.HandleRequest(clientRequest, new Connection(clientSocket));
            }
        }

        static private bool ReadToEnd(Socket socket, out byte[] receivedData)
        {
            byte[] buffer = new byte[socket.ReceiveBufferSize];
            int receivedBytes = 0;
            try
            {
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    while (socket.Poll(RESPONSE_TIMEOUT, SelectMode.SelectRead) && (receivedBytes = socket.Receive(buffer, buffer.Length, SocketFlags.None)) > 0)
                    {
                        memoryStream.Write(buffer, 0, receivedBytes);
                    }
                    receivedData = memoryStream.ToArray();
                    return true;
                }
            }
            catch
            {
                receivedData = null;
                return false;
            }
        }

        static public void AnswerToClient(byte[] message, Socket clientSocket)
        {
            if (clientSocket.Send(message, message.Length, SocketFlags.None) != message.Length)
            {
                WriteMessage("При отправке ответа клиенту произошла ошибка");
            }
        }

        static public Socket CreateRedirectionSocket(string host, int port)
        {
            Socket redirectionSocket = null;
            IPHostEntry receiverHostEntry = null;
            try
            {
                receiverHostEntry = Dns.GetHostEntry(host);
            }
            catch
            {
                WriteMessage("Не удалось определить IP-адрес по хосту {0}.", host);
                return redirectionSocket;
            }
            IPEndPoint receiverIPEndPoint = new IPEndPoint(receiverHostEntry.AddressList[0], port);
            redirectionSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            redirectionSocket.Connect(receiverIPEndPoint);
            return redirectionSocket;
        }

        static public void RedirectRequest(byte[] request, Connection connection)
        {
            if (connection.RedirectionSocket.Connected)
            {
                if (connection.RedirectionSocket.Send(request, request.Length, SocketFlags.None) != request.Length)
                {
                    WriteMessage("Данные хосту не были отправлены");
                }
                else
                {
                    byte[] response;
                    if (ReadToEnd(connection.RedirectionSocket, out response))
                    {
                        MessagesHandler.HandleResponse(response, connection);
                    }
                }
            }
        }

        public static void WriteMessage(string message, params object[] args)
        {
            Console.WriteLine(DateTime.Now.ToString() + " : " + message, args);
        }
    }
}
