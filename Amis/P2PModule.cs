using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Net;
using System.Net.Sockets;
using System.Windows;
using System.Net.NetworkInformation;

namespace Amis
{
    class P2PModule
    {
        private IPAddress selfIPv4Addr = null;
        private IPEndPoint serverEndPoint = null;
        private Socket socketToServer = null;
        public List<IPAddress> clients;

        public P2PModule()
        {
            selfIPv4Addr = GetIPv4();
            if (selfIPv4Addr == null)
            {
                throw new ArgumentNullException("Invalid network condition.");
            }

        }

        private IPAddress GetIPv4()
        {
            try
            {
                IPHostEntry IpEntry = Dns.GetHostEntry(Dns.GetHostName());
                for (int i = 0; i < IpEntry.AddressList.Length; i++)
                {
                    if (IpEntry.AddressList[i].AddressFamily == AddressFamily.InterNetwork)
                    {
                        return IpEntry.AddressList[i];
                    }
                }
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static List<int> GetBusyPorts()
        {
            IPGlobalProperties props = IPGlobalProperties.GetIPGlobalProperties();

            IPEndPoint[] tcpIPs = props.GetActiveTcpListeners();
            IPEndPoint[] udpIPs = props.GetActiveUdpListeners();

            TcpConnectionInformation[] tcpConnectionInformation = props.GetActiveTcpConnections();

            List<int> allPorts = new List<int>();
            foreach (IPEndPoint ep in tcpIPs) allPorts.Add(ep.Port);
            foreach (IPEndPoint ep in udpIPs) allPorts.Add(ep.Port);
            foreach (TcpConnectionInformation conn in tcpConnectionInformation) allPorts.Add(conn.LocalEndPoint.Port);

            return allPorts;
        }
    }
}
