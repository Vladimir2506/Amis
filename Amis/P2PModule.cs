using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
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
            try
            {
                listener = new TcpListener(theIP, IntraThreads.portNO);
            }
            catch(Exception e)
            {
                MessageBox.Show(e.Message, "对等方监听启动失败");
            }
        }

        public void SendData(byte[] data, string targetIP, int targetPort)
        {
            using (peer = new TcpClient())
            {
                peer.SendTimeout = 2000;
                peer.ReceiveTimeout = 2000;
                peer.SendBufferSize = bufferSize;
                try
                {
                    peer.Connect(targetIP, targetPort);
                }
                catch(Exception e)
                {
                    MessageBox.Show(e.Message, "对等方连接失败");
                    throw new Exception("NOL");
                }
                using (NetworkStream stream = peer.GetStream())
                {
                    try
                    {
                         stream.Write(data, 0, data.Length);
                    }
                    catch(Exception e)
                    {
                        MessageBox.Show(e.Message, "流写入错误");
                        return;
                    }
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
            try
            {
                listener.Start();
            }
            catch(Exception e)
            {
                MessageBox.Show(e.Message, "监听启动失败");
            }
            threadRecv.Start();
        }

        public void EndListen()
        {
            lock (inter)
            {
                inter.listening = false;
            }
            threadRecv.Join();
            try
            {
                listener.Stop();
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "监听终止失败");
            }
        }

        private void AcceptRecv()
        {
            bool ongoing = true;
            while (ongoing)
            {
                if (listener.Pending())
                {
                    TcpClient client = null;
                    try
                    {
                        client = listener.AcceptTcpClient();
                    }
                    catch(Exception e)
                    {
                        MessageBox.Show(e.Message, "对等方接受连接失败");
                    }
                    client.ReceiveBufferSize = bufferSize;
                    using (NetworkStream stream = client.GetStream())
                    {
                        int small = 1024 * 1024, len = 0;
                        byte[] buff = new byte[small];
                        if (stream.CanRead)
                        {
                            do
                            {
                                int actual = 0;
                                try
                                {
                                    actual = stream.Read(buff, 0, small);
                                }
                                catch(Exception e)
                                {
                                    MessageBox.Show(e.Message, "流读出错误");
                                }
                                Buffer.BlockCopy(buff, 0, recvBuffer, len, actual);
                                len += actual;
                                Thread.Sleep(50);
                            } while (stream.DataAvailable);
                        }
                        stream.Close();
                        byte[] msg = new byte[len];
                        Buffer.BlockCopy(recvBuffer, 0, msg, 0, len);
                        lock (inter)
                        {
                            inter.messages.Enqueue(msg);
                        }
                    }        
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
