using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Amis
{
    public class P2PModule
    {
        private const int bufferSize = 4096;
        private const int fileBufferSize = 65536;

        private Thread threadRecv = null;
        private InterThreads inter = null;
        private IntraThreads intra = null;
        private byte[] recvBuffer = null;

        private TcpListener listener = null;
        private TcpClient peer = null;
        public IPAddress theIP = null;

        private static P2PModule instance = null;

        public static P2PModule GetInstance()
        {
            if(instance == null)
            {
                instance = new P2PModule();
            }
            return instance;
        }

        private P2PModule()
        {
            inter = InterThreads.GetInstance();
            intra = IntraThreads.GetInstance();
            theIP = GetIPV4();
            listener = new TcpListener(theIP, IntraThreads.portNO);
            peer = new TcpClient();
        }

        public void SendData(byte[] data, string targetIP, int targetPort)
        {
            peer.Connect(targetIP, targetPort);
            NetworkStream stream = peer.GetStream();
            stream.Write(data, 0, data.Length);
            stream.Close();
            peer.Close();
        }

        public void BeginListen()
        {
            threadRecv = new Thread(AcceptRecv)
            {
                Name = "MyNetMessage"
            };
            lock (inter)
            {
                inter.listening = true;
            }
            listener.Start();
            threadRecv.Start();
        }

        public void EndListen()
        {
            lock (inter)
            {
                inter.listening = false;
            }
            threadRecv.Join();
            listener.Stop();
        }

        private void AcceptRecv()
        {
            bool ongoing = true;
            while (ongoing)
            {
                if (listener.Pending())
                {
                    TcpClient client = listener.AcceptTcpClient();
                    NetworkStream stream = client.GetStream();
                    if (stream.DataAvailable)
                    {
                        recvBuffer = new byte[bufferSize];
                        int len = stream.Read(recvBuffer, 0, bufferSize);
                        byte[] msg = new byte[len];
                        Buffer.BlockCopy(recvBuffer, 0, msg, 0, len);
                        lock (inter)
                        {
                            inter.messages.Enqueue(msg);
                        }
                    }
                    stream.Close();
                    client.Close();
                }
                lock (inter)
                {
                    ongoing = inter.listening;
                }
            }
        }

        private IPAddress GetIPV4()
        {
            try
            {
                string HostName = Dns.GetHostName();
                IPHostEntry IpEntry = Dns.GetHostEntry(HostName);
                for (int i = 0; i < IpEntry.AddressList.Length; i++)
                {

                    if (IpEntry.AddressList[i].AddressFamily == AddressFamily.InterNetwork)
                    {
                        return IpEntry.AddressList[i];
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                MessageBox.Show("获取本机IP出错:" + ex.Message);
                return null;
            }
        }
    }
}
