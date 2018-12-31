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
    public class CSModule
    {
        private IPEndPoint serverEndPoint = null;
        private Socket socketToServer = null;

        public const string ipServer = "166.111.140.14";
        public const int portServer = 8000;
        private const int bufferSize = 64;

        private static CSModule instance = null;

        public static CSModule GetInstance()
        {
            if (instance == null)
            {
                instance = new CSModule();
            }
            return instance;
        }

        private CSModule()
        {
            serverEndPoint = new IPEndPoint(IPAddress.Parse(ipServer), portServer);
        }
        
        public string QueryOnce(string msg)
        {
            string recv = "NRP";
            socketToServer = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                socketToServer.Connect(serverEndPoint);
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "发起连接错误");
            }
            try
            {
                byte[] toSend = Encoding.UTF8.GetBytes(msg);
                socketToServer.Send(toSend);
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "发送错误");
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
                MessageBox.Show(e.Message, "接收错误");
                return recv;
            }
            try
            {
                socketToServer.Disconnect(true);
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "断开连接错误");
            }
            socketToServer.Close();
            return recv;
        }
    }
}
