using System;

namespace ProxyServer
{
    public class Header_Host : Header
    {
        public string Host;
        public int Port;

        public Header_Host(string value, string host, int port) : base(value)
        {
            Host = host;
            Port = port;
        }
    }
}
