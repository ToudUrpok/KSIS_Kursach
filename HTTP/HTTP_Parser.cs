using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace ProxyServer
{
    public class HTTP_Parser
    {
        private static Dictionary<string, string> RegularExpressions = new Dictionary<string, string>
        {
            { "headerHost", @"^(((?<host>.+?):(?<port>\d+?))|(?<host>.+?))$" },
            { "requestStartingLine", @"(?<method>.+)\s+(?<uri>.+)\s+HTTP/(?<version>[\d\.]+)$" },
            { "responseStartingLine", @"HTTP/(?<version>[\d\.]+)\s+(?<code>\d+)\s*(?<phrase>.*)$" },
            { "messageHeaders", @"^(?<name>[^\x3A]+)\:\s{1}(?<value>.+)$" },
            { "headerContentType", @"(?<charset>.+?)=((""(?<value>.+?)"")|((?<value>[^\;]+)))[\;]{0,1}" }
        };

        static private int DEFAULT_PORT = 80;
        static public int PARSING_FAILED_CODE = -1;
        static private int MESSAGE_HAS_NO_HEADERS_CODE = 0;
        static private string HEADERS_BLOCK_SEPARATOR = "\r\n";
        static private char[] HEADERS_SEPARATOR = { '\r', '\n', ' ' };
        static private string BODY_SEPARATOR = "\r\n\r\n";

        public  Universal_Dictionary ParsedMessages;
        public  GZipArchiver MessageBodyArchiver;

        public HTTP_Parser()
        {
            ParsedMessages = new Universal_Dictionary();
            MessageBodyArchiver = new GZipArchiver();

        }

        private void ParseHeaderHost(string headerValue, out string host, out int port)
        {
            Regex regex = new Regex(RegularExpressions["headerHost"]);
            Match match = regex.Match(headerValue);
            if (match.Success)
            {
                host = match.Groups["host"].Value;
                if (!int.TryParse(match.Groups["port"].Value, out port))
                {
                    port = DEFAULT_PORT;
                }      
            }
            else
            {
                host = string.Empty;
                port = PARSING_FAILED_CODE;
            }
        }

        private void ParseHeaderContentType(string headerValue,  out string charset)
        {
            Regex regex = new Regex(RegularExpressions["headerContentType"], RegexOptions.Singleline);
            MatchCollection matchCollection = regex.Matches(headerValue);
            charset = string.Empty;
            foreach (Match match in matchCollection)
            {
                if (match.Groups["charset"].Value.Trim().ToLower() == "charset")
                {
                    charset = match.Groups["value"].Value;
                }
            }
        }

        private int GetMessageHeaders(ref string stringMessage, ref HTTP_Message message)
        {
            int headersEnding;
            int startingLineEnding = stringMessage.IndexOf(HEADERS_BLOCK_SEPARATOR);
            if (startingLineEnding != PARSING_FAILED_CODE)
            {
                headersEnding = stringMessage.IndexOf(BODY_SEPARATOR);
                if (headersEnding != PARSING_FAILED_CODE)
                {
                    int headersBegining = startingLineEnding + 2;
                    Regex regex = new Regex(RegularExpressions["messageHeaders"], RegexOptions.Multiline);
                    MatchCollection matchCollection =
                        regex.Matches(stringMessage.Substring(headersBegining, headersEnding - headersBegining));
                    if (matchCollection.Count > 0)
                    {
                        stringMessage = stringMessage.Substring(0, headersBegining - 2);
                        message.Headers = new HTTP_Message_Headers();
                        foreach (Match match in matchCollection)
                        {
                            string name = match.Groups["name"].Value;
                            if (!message.Headers.HeadersDictionary.ContainsKey(name))
                            {
                                if (name.Trim().ToLower() == "host")
                                {
                                    string host;
                                    int port;
                                    string headerValue = match.Groups["value"].Value.Trim(HEADERS_SEPARATOR);
                                    ParseHeaderHost(headerValue, out host, out port);
                                    message.Headers.HeadersDictionary.Add(name, new Header_Host(headerValue, host, port));
                                }
                                else if (name.Trim().ToLower() == "content-type")
                                {
                                    string headerValue = match.Groups["value"].Value.Trim(HEADERS_SEPARATOR);
                                    if (!string.IsNullOrEmpty(headerValue))
                                    {
                                        string charset;
                                        ParseHeaderContentType(headerValue, out charset);
                                        message.Headers.HeadersDictionary.Add(name, new Header_ContentType(headerValue, charset));
                                    }
                                }
                                else
                                {
                                    message.Headers.HeadersDictionary.Add(name, new Header(match.Groups["value"].Value.Trim(HEADERS_SEPARATOR)));
                                }
                            }
                        }
                    }
                    else
                    {
                        headersEnding = MESSAGE_HAS_NO_HEADERS_CODE;
                    }
                }
            }
            else
            {
                headersEnding = MESSAGE_HAS_NO_HEADERS_CODE;
            }
            return headersEnding;
        }

        private byte[] GetMessageBody(int messageKey, int headersEndingPosition)
        {
            var message = ParsedMessages.GetElement(messageKey) as HTTP_Message;
            byte[] messageBody = new byte[message.SourceMessage.Length - headersEndingPosition - BODY_SEPARATOR.Length];
            Buffer.BlockCopy(message.SourceMessage, headersEndingPosition + BODY_SEPARATOR.Length,
                messageBody, 0, messageBody.Length);
            if (message.Headers.HeadersDictionary.ContainsKey("Content-Encoding") && 
                message.Headers.HeadersDictionary["Content-Encoding"].Value.ToLower() == "gzip")
            {
                return MessageBodyArchiver.Decompress(messageBody);
            }
            else
            {
                return messageBody;
            }
        }

        private void SetEncoding(HTTP_Message message, out Encoding encoding)
        {
            encoding = Encoding.UTF8;
            if (message.Headers.HeadersDictionary.ContainsKey("Content-Type"))
            {
                var contentType = message.Headers.HeadersDictionary["Content-Type"] as Header_ContentType;
                if (!String.IsNullOrEmpty(contentType?.Charset))
                {
                    try
                    {
                        encoding = Encoding.GetEncoding(contentType?.Charset);
                    }
                    catch
                    { }
                }
            }
        }

        public string GetMessageBodyAsString(int messageKey)
        {
            var message = ParsedMessages.GetElement(messageKey) as HTTP_Message;
            Encoding encoding;
            SetEncoding(message, out encoding);
            return encoding.GetString(message.Body);
        }

        public void ChangeBody(string newBody, int messageKey)
        {
            var message = ParsedMessages.GetElement(messageKey) as HTTP_Message;
            Encoding encoding;
            SetEncoding(message, out encoding);
            byte[] newBodyBytes;
            if (message.Headers.HeadersDictionary.ContainsKey("Content-Encoding") &&
                message.Headers.HeadersDictionary["Content-Encoding"].Value.ToLower() == "gzip")
            {
                newBodyBytes = MessageBodyArchiver.Compress(encoding.GetBytes(newBody));
            }
            else
            {
                newBodyBytes = encoding.GetBytes(newBody);
            }
            message.Body = newBodyBytes;
            string commonPart = String.Format("HTTP/{0} {1} {2}\r\n", message.StartingLine.InitialPart, message.StartingLine.MiddlePart, message.StartingLine.EndingPart);
            foreach (KeyValuePair<string, Header> header in message.Headers.HeadersDictionary)
            {
                if (header.Key.ToLower() == "content-length")
                {
                    commonPart += String.Format("{0}: {1}", header.Key, newBodyBytes.Length);
                }
                else
                {
                    commonPart += String.Format("{0}: {1}", header.Key, header.Value.Value);
                }
            }
            commonPart += "\r\n\r\n";
            byte[] commonPartBytes = Encoding.ASCII.GetBytes(commonPart);
            byte[] result = new byte[commonPartBytes.Length + newBodyBytes.Length];
            for (int i = 0; i < commonPartBytes.Length; i++)
            {
                result[i] = commonPartBytes[i];
            }
            for (int i = commonPartBytes.Length; i < result.Length; i++)
            {
                result[i] = newBodyBytes[i- commonPartBytes.Length];
            }
            message.SourceMessage = result;
        }

        private bool ParseCommonPart(byte[] sourceMessage, out string messageStartingLine, out int newMessageKey)
        {
            messageStartingLine = null;
            string stringMessage = GetStringFromBytes(sourceMessage, Encoding.ASCII.EncodingName); 
            HTTP_Message newMessage = new HTTP_Message(sourceMessage);
            newMessageKey = ParsedMessages.AddElement(newMessage);
            int messageHeadersEnding = GetMessageHeaders(ref stringMessage, ref newMessage);
            if (messageHeadersEnding != MESSAGE_HAS_NO_HEADERS_CODE)
            {
                messageStartingLine = stringMessage;
                if (messageHeadersEnding != PARSING_FAILED_CODE)
                {
                    newMessage.Body = GetMessageBody(newMessageKey, messageHeadersEnding);
                }
                return true;
            }
            else
            {
                return false;
            }
        }

        private bool IsHostHeaderAvailable(int messageKey)
        {
            var message = ParsedMessages.GetElement(messageKey) as HTTP_Message;
            foreach (var header in message.Headers.HeadersDictionary)
            {
                if (header.Key.Trim().ToLower() == "host")
                {
                    Header_Host host = header.Value as Header_Host;
                    if (host == null)
                    {
                        return false;
                    }

                    if ((host.Host == string.Empty) || (host.Port == PARSING_FAILED_CODE))
                    {
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private bool ValidHTTPRequest(int messageKey)
        {
            bool IsValid;
            IsValid = IsHostHeaderAvailable(messageKey);
            return IsValid;
        }

        public int ParseHTTPRequest(byte[] sourceMessage)
        { 
            int createdMessageKey;
            string messageStartingLine;
            if (ParseCommonPart(sourceMessage, out messageStartingLine, out createdMessageKey))
            {
                if (ValidHTTPRequest(createdMessageKey))
                {
                    Regex regex = new Regex(RegularExpressions["requestStartingLine"]);
                    Match match = regex.Match(messageStartingLine);
                    if (match.Success)
                    {
                        var message = ParsedMessages.GetElement(createdMessageKey) as HTTP_Message;
                        message.StartingLine =
                            new HTTP_Message_Starting_Line(match.Groups["method"].Value.ToUpper(),
                            match.Groups["uri"].Value, match.Groups["version"].Value);
                        return createdMessageKey;
                    }
                    else
                    {
                        ParsedMessages.RemoveElement(createdMessageKey);
                        return PARSING_FAILED_CODE;
                    }
                }
                else
                {
                    ParsedMessages.RemoveElement(createdMessageKey);
                    return PARSING_FAILED_CODE;
                }
            }
            else
            {

                return PARSING_FAILED_CODE;
            }
        }

        public int ParseHTTPResponse(byte[] sourceMessage)
        {
            int createdMessageKey;
            string messageStartingLine;
            if (ParseCommonPart(sourceMessage, out messageStartingLine, out createdMessageKey))
            {
                Regex regex = new Regex(RegularExpressions["responseStartingLine"]);
                Match match = regex.Match(messageStartingLine);
                if (match.Success)
                {
                    var message = ParsedMessages.GetElement(createdMessageKey) as HTTP_Message;
                    message.StartingLine =
                        new HTTP_Message_Starting_Line(match.Groups["version"].Value.ToUpper(),
                        match.Groups["code"].Value, match.Groups["phrase"].Value);
                    return createdMessageKey;
                }
                else
                {
                    return PARSING_FAILED_CODE;
                }
            }
            else
            {
                return PARSING_FAILED_CODE;
            }
        }

        public string GetStringFromBytes(byte[] bytes, string encodingName)
        {
            if (bytes != null)
            {
                return Encoding.GetEncoding(encodingName).GetString(bytes);
            }
            else
            {
                return null;
            }
        }
    }
}
