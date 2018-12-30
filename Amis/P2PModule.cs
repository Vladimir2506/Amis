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
            try
            {
                socketPeer.BeginConnect(new IPEndPoint(IPAddress.Parse(targetIP), targetPort), SendDataAsync2, socketPeer);
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "发起连接错误");
            }
        }

        public void SendDataAsync2(IAsyncResult ar)
        {
            Socket selfSocket = (Socket)ar.AsyncState;
            try
            {
                selfSocket.EndConnect(ar);
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "连接错误");
            }
            try
            {
                selfSocket.BeginSend(sendBuffer, 0, sendBuffer.Length, SocketFlags.None, SendDataAsync3, selfSocket);
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "启动发送错误");
            }
        }

        private void SendDataAsync3(IAsyncResult ar)
        {
            Socket selfSocket = (Socket)ar.AsyncState;
            try
            {
                selfSocket.EndSend(ar);
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "发送错误");
            }
        }

        public void BeginListen(int portNO, int backlog)
        {
            socketListen.Bind(new IPEndPoint(GetIPV4(), portNO));
            try
            {
                socketListen.Listen(backlog);
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "监听错误");
            }
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
                try
                {
                    socketListen.BeginAccept(AcceptRecvAsync2, socketListen);
                }
                catch(Exception e)
                {
                    MessageBox.Show(e.Message, "启动接受错误");
                }
                lock (inters)
                {
                    ongoing = inters.listening;
                }
            }
        }

        public void AcceptRecvAsync2(IAsyncResult ar)
        {
            Socket selfSocket = (Socket)ar.AsyncState;
            try
            {
                Socket recvSocket = selfSocket.EndAccept(ar);
                recvBuffer = new byte[bufferSize];
                try
                {
                    recvSocket.BeginReceive(recvBuffer, 0, bufferSize, SocketFlags.None, AcceptRecvAsync3, recvSocket);
                }
                catch (Exception e)
                {
                    MessageBox.Show(e.Message, "发起接收错误");
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "接受错误");
            }
            
        }

        public void AcceptRecvAsync3(IAsyncResult ar)
        {
            Socket recvSocket = (Socket)ar.AsyncState;
            try
            {
                int len = recvSocket.EndReceive(ar);
                lock (inters)
                lock (recvBuffer)
                {
                    byte[] msg = new byte[len];
                    Buffer.BlockCopy(recvBuffer, 0, msg, 0, len);
                    inters.messages.Append(msg);
                }
            }
            catch(Exception e)
            {
                MessageBox.Show(e.Message, "接收错误");
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
}
