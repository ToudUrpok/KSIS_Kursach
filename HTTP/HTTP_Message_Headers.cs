using System;
using System.Collections.Generic;

namespace ProxyServer
{
    public class HTTP_Message_Headers
    {
        public Dictionary<string, Header> HeadersDictionary;

        public HTTP_Message_Headers()
        {
            HeadersDictionary = new Dictionary<string, Header>();
        }
    }
}
