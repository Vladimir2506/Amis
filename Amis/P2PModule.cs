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
    class P2PModule
    {
        private const int bufferSize = 65536;

        private Socket socketListen = null;
        private Socket socketPeer = null;
        private Thread threadRecv = null;
        private InterThreads inters = null;
        private byte[] recvBuffer = null;
        private byte[] sendBuffer = null;

        public P2PModule()
        {
            inters = InterThreads.GetInstance();
            socketListen = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socketPeer = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            threadRecv = new Thread(AcceptRecvAsync1);
        }

        public void SendDataAsync(byte[] data, string targetIP, int targetPort)
        {
            socketPeer.BeginConnect(new IPEndPoint(IPAddress.Parse(targetIP), targetPort), SendDataAsync2, socketPeer);
        }

        public void SendDataAsync2(IAsyncResult ar)
        {
            Socket selfSocket = (Socket)ar.AsyncState;
            selfSocket.EndConnect(ar);
            selfSocket.BeginSend(sendBuffer, 0, sendBuffer.Length, SocketFlags.None, SendDataAsync3, selfSocket);
        }

        private void SendDataAsync3(IAsyncResult ar)
        {
            Socket selfSocket = (Socket)ar.AsyncState;
            selfSocket.EndSend(ar);
        }

        public void BeginListen(int portNO ,int backlog)
        {
            socketListen.Bind(new IPEndPoint(GetIPV4(), portNO));
            socketListen.Listen(backlog);
            inters.listening = true;
            threadRecv.Start();
        }

        public void EndListen()
        {
            lock (inters)
            {
                inters.listening = false;
            }
            threadRecv.Join();
            socketListen.Close();
        }

        private void AcceptRecvAsync1()
        {
            bool ongoing = true;
            while (ongoing)
            {
                socketListen.BeginAccept(AcceptRecvAsync2, socketListen);
                lock (inters)
                {
                    ongoing = inters.listening;
                }
            }
        }

        public void AcceptRecvAsync2(IAsyncResult ar)
        {
            Socket selfSocket = (Socket)ar.AsyncState;
            Socket recvSocket = selfSocket.EndAccept(ar);
            recvBuffer = new byte[bufferSize];
            recvSocket.BeginReceive(recvBuffer, 0, bufferSize, SocketFlags.None, AcceptRecvAsync3, recvSocket);
        }

        public void AcceptRecvAsync3(IAsyncResult ar)
        {
            Socket recvSocket = (Socket)ar.AsyncState;
            int len = recvSocket.EndReceive(ar);
            lock(inters)
            {
                byte[] msg = new byte[len];
                Buffer.BlockCopy(recvBuffer, 0, msg, 0, len);
                inters.messages.Append(msg);
            }
            recvSocket.BeginDisconnect(true, AcceptRecvAsync4, recvSocket);
        }

        public void AcceptRecvAsync4(IAsyncResult ar)
        {
            Socket recvSocket = (Socket)ar.AsyncState;
            recvSocket.EndDisconnect(ar);
            recvSocket.Close();
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

    class InterThreads
    {
        private static InterThreads instance = null;
        public bool listening = false;
        public bool processing = false;
        public Queue<byte[]> messages = null;

        private InterThreads()
        {
            messages = new Queue<byte[]>();
        }

        public static InterThreads GetInstance()
        {
            if(instance == null)
            {
                instance = new InterThreads();
            }
            return instance;
        }
    }
}
