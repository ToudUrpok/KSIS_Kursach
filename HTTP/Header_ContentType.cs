using System;

namespace ProxyServer
{
    public class Header_ContentType : Header
    {
        public string Charset;

        public Header_ContentType(string headerValue, string charset)
            : base(headerValue)
        {
            Charset = charset;
        }
    }
}
