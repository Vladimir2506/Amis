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
using System.Windows.Navigation;
using System.Windows.Shapes;

using System.Windows.Threading;

namespace Amis
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private CSModule connectionToServer = null;
        private string result = null;
        private string userID = null;
        private string passwd = null;
        private DispatcherTimer timerDelayShutdown = null;
        
        public MainWindow()
        {
            InitializeComponent();
        }

        private void AmisLogin_Loaded(object sender, RoutedEventArgs e)
        {
            connectionToServer = new CSModule("166.111.140.14", 8000);
        }

        private void AmisLogin_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            //result = connectionToServer.QueryOnce("logout"+userID);
            connectionToServer.Release();
        }

        private void BtnExit_Click(object sender, RoutedEventArgs e)
        {
            timerDelayShutdown = new DispatcherTimer();
            timerDelayShutdown.Tick += new EventHandler(DelayShutdown);
            timerDelayShutdown.Interval = TimeSpan.FromMilliseconds(200);
            timerDelayShutdown.Start();
        }

        private void DelayShutdown(object sender, EventArgs e)
        {
            timerDelayShutdown.Stop();
            Close();
        }

        private void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            userID = tbUsername.Text;
            passwd = tbPassword.Password;
            /*result = connectionToServer.QueryOnce(userID + "_" + passwd);
            if (result == "lol")
            {
                lblMessage.Content = "登陆成功！";
                lblMessage.Foreground = new SolidColorBrush(Color.FromArgb(222, 0, 0, 0));
            }
            else
            {
                lblMessage.Content = "登陆失败！";
                lblMessage.Foreground = new SolidColorBrush(Color.FromArgb(222, 0xe5, 0x39, 0x35));
            }*/
            
        }

        private void RectTitlebar_MouseMove(object sender, MouseEventArgs e)
        {
            if(e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }
    }
}
