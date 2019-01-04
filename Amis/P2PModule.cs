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
        private const int bufferSize = 16 * 1024 * 1024;

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
            recvBuffer = new byte[bufferSize];

            inter = InterThreads.GetInstance();
            intra = IntraThreads.GetInstance();
            theIP = GetIPV4();
            listener = new TcpListener(theIP, IntraThreads.portNO);
        }

        public void SendData(byte[] data, string targetIP, int targetPort)
        {
            using (peer = new TcpClient())
            {
                peer.SendBufferSize = bufferSize;
                peer.Connect(targetIP, targetPort);
                using (NetworkStream stream = peer.GetStream())
                {
                    stream.Write(data, 0, data.Length);
                    stream.Close();
                }
                peer.Close();
            }
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
                    client.ReceiveBufferSize = bufferSize;
                    NetworkStream stream = client.GetStream();
                    int small = 1024, len = 0;
                    byte[] buff = new byte[small];
                    if(stream.CanRead)
                    {
                        do
                        {
                            int actual = stream.Read(buff, 0, small);
                            Buffer.BlockCopy(buff, 0, recvBuffer, len, actual);
                            len += actual;
                        } while (stream.DataAvailable);
                    }
                    byte[] msg = new byte[len];
                    Buffer.BlockCopy(recvBuffer, 0, msg, 0, len);
                    lock (inter)
                    {
                        inter.messages.Enqueue(msg);
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
