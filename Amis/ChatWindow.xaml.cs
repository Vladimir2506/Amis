using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Threading;

namespace Amis
{
    /// <summary>
    /// ChatWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ChatWindow : Window
    {
        private InterThreads inter = null;
        private IntraThreads intra = null;
        private CSModule csCore = null;
        private P2PModule p2pCore = null;
        private Thread threadPump = null;

        public ChatWindow()
        {
            InitializeComponent();

            inter = InterThreads.GetInstance();
            intra = IntraThreads.GetInstance();

            intra.monAlias = "夏卓凡";

            p2pCore = P2PModule.GetInstance();
            csCore = CSModule.GetInstance();
            threadPump = new Thread(Relay)
            {
                Name = "MyMessagePump"
            };
        }

        private void AmisChat_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            p2pCore.EndListen();
            lock(inter)
            {
                inter.processing = false;
            }
            threadPump.Join();
            string res = csCore.QueryOnce("logout" + intra.monId);
            // TODO:Saving ...

            Application.Current.MainWindow.Show();
        }

        private void LbiQuit_Selected(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void AmisChat_Loaded(object sender, RoutedEventArgs e)
        {

            // TODO:Loading data...

            tbSelfInfo.Inlines.Add(new Run(intra.monId));
            tbSelfInfo.Inlines.Add(new LineBreak());
            tbSelfInfo.Inlines.Add(new Run(intra.monAlias));

            // TODO:Set up message loop
            p2pCore.BeginListen();
            threadPump.Start();
        }

        private void CzTop_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void Relay()
        {
            bool ongoing = true;
            while(ongoing)
            {
                byte[] msg = null;
                lock(inter)
                {
                    if (inter.messages.Count > 0) msg = inter.messages.Dequeue();
                }
                if (msg != null && msg.Length > 0)
                {
                    Dispatcher.BeginInvoke(new LogicProc(MainLogic), msg);
                }
                lock(inter)
                {
                    ongoing = inter.processing;
                }
            }
        }

        private delegate void LogicProc(byte[] msg);

        private void MainLogic(byte[] msg)
        {
            // TODO: Protocol                        
        }
    }
}
