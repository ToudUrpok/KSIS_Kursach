using System;
using System.Net;

namespace ProxyServer
{
    class Host_Network_Information
    {
        public IPAddress HostIPAddress { get; }

        public Host_Network_Information()
        {
            HostIPAddress = DefineHostIPAddress();
        }

        private static int IPv4_ADDRESS_LENGTH = 4;
        private static IPAddress LOCAL_HOST_IPAddress = IPAddress.Parse("127.0.0.1");
        private IPAddress DefineHostIPAddress()
        {
            IPAddress[] hostAddresses = Dns.GetHostAddresses("");
            foreach (var address in hostAddresses)
            {
                if (address.GetAddressBytes().Length == IPv4_ADDRESS_LENGTH)
                {
                    return address;
                }
            }
            return LOCAL_HOST_IPAddress;
        }
    }
}
