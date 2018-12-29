using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Net;
using System.Net.Sockets;
using System.Windows;

namespace Amis
{
    class CSModule
    {
        private const int bufferSize = 1024;

        private Socket socketToServer = null;

        public CSModule(string ipAddress, int portNO)
        {
            IPEndPoint serverEndPoint = new IPEndPoint(IPAddress.Parse(ipAddress), portNO);
            socketToServer = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                socketToServer.Connect(serverEndPoint);
            }
            catch (Exception e)
            {
                MessageBox.Show(e.ToString(), "发起连接错误");
            }
        }

        public void Release()
        {
            try
            {
                socketToServer.Disconnect(true);
                socketToServer.Close();
            }
            catch (Exception e)
            {
                MessageBox.Show(e.ToString(), "断开连接错误");
            }
        }

        public string QueryOnce(string msg)
        {
            string recv = "NRP";

            try
            {
                byte[] toSend = Encoding.UTF8.GetBytes(msg);
                socketToServer.Send(toSend);
            }
            catch (Exception e)
            {
                MessageBox.Show(e.ToString(), "发送错误");
                return recv;
            }

            try
            {
                byte[] buffer = new byte[bufferSize];
                int recvLength = socketToServer.Receive(buffer);
                recv = Encoding.UTF8.GetString(buffer, 0, recvLength);
            }
            catch(Exception e)
            {
                MessageBox.Show(e.ToString(), "接收错误");
                return recv;
            }

            return recv;
        }
    }
}
