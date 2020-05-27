using System;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;

namespace ProxyServer
{
    public class Messages_Handler
    {
        private bool LogIn(Connection connection, HTTP_Message request)
        {
            if (Server.NEED_AUTHORIZATION)
            {
                if (!request.Headers.HeadersDictionary.ContainsKey("Authorization"))
                {
                    Server.AnswerToClient(CreateHTTPErrorMessage(401, "Unauthorized"), connection.ClientSocket);
                    return false;
                }
                else
                {
                    string authorizationHeader = Encoding.UTF8.GetString(Convert.FromBase64String(request.Headers.HeadersDictionary["Authorization"].Value.Replace("Basic ", "")));
                    string login = authorizationHeader.Split(":".ToCharArray())[0];
                    string password = authorizationHeader.Split(":".ToCharArray())[1];
                    if (!Server.VarifyAuthorizationData(login, password))
                    {
                        Server.AnswerToClient(CreateHTTPErrorMessage(401, "Unauthorized"), connection.ClientSocket);
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                }
            }
            else
            {
                return true;
            }
        }

        public void HandleRequest(byte[] clientRequest, Connection connection)
        {
            Server.WriteMessage("Получен запрос {0} байт", clientRequest.Length);

                int requestKey = Server.HTTPParser.ParseHTTPRequest(clientRequest);
                if (requestKey != HTTP_Parser.PARSING_FAILED_CODE)
                {
                    var request = Server.HTTPParser.ParsedMessages.GetElement(requestKey) as HTTP_Message;
                    var headerHost = (Header_Host)request.Headers.HeadersDictionary["Host"];
                    Server.WriteMessage("http-запрос {0} , метод {1}, хост {2}:{3}", requestKey, request.StartingLine.InitialPart, headerHost.Host, headerHost.Port);

                    if (!LogIn(connection, request))
                    {
                        return;
                    }

                    if (Server.ALLOW_FORBIDDEN_DOMAINS &&  Server.VarifyForbiddenDomain(headerHost.Host.ToLower()))
                    {
                        Server.AnswerToClient(CreateHTTPErrorMessage(403, "Forbidden"), connection.ClientSocket);
                        return;
                    }

                    if (request.StartingLine.InitialPart.ToLower() == "connect")
                    {
                        Server.AnswerToClient(CreateHTTPErrorMessage(501, "Not Implemented"), connection.ClientSocket);
                        Server.WriteMessage("Метод connect не поддерживается.");
                        Server.HTTPParser.ParsedMessages.RemoveElement(requestKey);
                        connection.CloseConnection();
                        connection = null;
                    }
                    else
                    {
                        connection.RedirectionSocket = Server.CreateRedirectionSocket(headerHost.Host, headerHost.Port);
                        if (connection.RedirectionSocket != null)
                        {
                            Server.RedirectRequest(request.SourceMessage, connection);
                        }
                    }
                }
                else
                {
                    Server.AnswerToClient(CreateHTTPErrorMessage(400, "Bad Request"), connection.ClientSocket);
                    connection.CloseConnection();
                    connection = null;
                }
        }

        private void AppendResponse(HTTP_Message response, int responseKey, Connection connection)
        {
            switch (int.Parse(response.StartingLine.MiddlePart))
            {
                case 400:
                case 401:
                case 403:
                case 404:
                case 501:
                    Server.AnswerToClient(CreateHTTPErrorMessage(int.Parse(response.StartingLine.MiddlePart), response.StartingLine.EndingPart), connection.ClientSocket);
                    break;

                default:
                    if (Server.APPEND_HTML_PAGES)
                    {
                        if (response.Headers.HeadersDictionary.ContainsKey("Content-Type") && (response.Headers.HeadersDictionary["Content-Type"].Value == "text/html"))
                        {
                            string body = Server.HTTPParser.GetMessageBodyAsString(responseKey);
                            body = Regex.Replace(body, "<title>(?<title>.*?)</title>", "<title>ProxyServer - $1</title>");
                            body = Regex.Replace(body, "(<body.*?>)", "$1<div style='height:20px;width:100%;background-color:black;color:white;font-weight:bold;text-align:center;'>Proxy Server</div>");
                            Server.HTTPParser.ChangeBody(body, responseKey);
                        }
                        Server.AnswerToClient(response.SourceMessage, connection.ClientSocket);
                    }
                    break;
            }
        }

        public void HandleResponse(byte[] responseBytes, Connection connection)
        {
            Server.WriteMessage("Получен ответ {0} байт", responseBytes.Length);
            int responseKey = Server.HTTPParser.ParseHTTPResponse(responseBytes);
            if (responseKey != HTTP_Parser.PARSING_FAILED_CODE)
            {
                var response = Server.HTTPParser.ParsedMessages.GetElement(responseKey) as HTTP_Message;
                Server.WriteMessage("http-ответ {0} , код состояния {1}", responseKey, response.StartingLine.MiddlePart);

                AppendResponse(response, responseKey, connection);
            }
            else 
            {
                Server.AnswerToClient(responseBytes, connection.ClientSocket);
            }
            connection.CloseConnection();
            connection = null;
            Server.HTTPParser.ParsedMessages.RemoveElement(responseKey);
        }

        private byte[] CreateHTTPErrorMessage(int statusCode, string reasonPhrase)
        {
            FileInfo fileInfo = new FileInfo(String.Format("MistakesTemplates/HTTP{0}.htm", statusCode));
            byte[] headers = Encoding.ASCII.GetBytes(String.Format("HTTP/1.1 {0} {1}\r\n{3}Content-Type: text/html\r\nContent-Length: {2}\r\n\r\n", statusCode, reasonPhrase, fileInfo.Length, (statusCode == 401 ? "WWW-Authenticate: Basic realm=\"ProxyServer\"\r\n" : "")));
            byte[] result = null;
            using (FileStream fileStream = new FileStream(fileInfo.FullName, FileMode.Open, FileAccess.Read))
            {
                using (BinaryReader binaryReader = new BinaryReader(fileStream, Encoding.UTF8))
                {
                    result = new byte[headers.Length + fileStream.Length];
                    Buffer.BlockCopy(headers, 0, result, 0, headers.Length);
                    Buffer.BlockCopy(binaryReader.ReadBytes(Convert.ToInt32(fileStream.Length)), 0, result, headers.Length, Convert.ToInt32(fileStream.Length));
                }
            }
            return result;
        }
    }
}
