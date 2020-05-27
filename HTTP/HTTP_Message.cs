using System;

namespace ProxyServer
{
    public enum HTTPMessageType { httpRequest, httpResponse }
    public class HTTP_Message
    {
        public byte[] SourceMessage;

        public HTTP_Message_Starting_Line StartingLine;
        
        public HTTP_Message_Headers Headers;
        
        public byte[] Body;
       
        public HTTP_Message(byte[] sourceMessage)
        {
            SourceMessage = sourceMessage;
            Headers = null;
            Body = null;
            StartingLine = null;
        }
    }
}
