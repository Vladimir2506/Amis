using MaterialDesignThemes.Wpf;
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
        private CSModule csCore = null;
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
            csCore = CSModule.GetInstance();
        }

        private void AmisLogin_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            
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
            DoLogin();
        }

        private void RectTitlebar_MouseMove(object sender, MouseEventArgs e)
        {
            if(e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void TbPassword_KeyDown(object sender, KeyEventArgs e)
        {
            if(e.Key == Key.Enter)
            {
                DoLogin();
            }
        }

        private void DoLogin()
        {
            userID = tbUsername.Text;
            passwd = tbPassword.Password;
            result = csCore.QueryOnce(userID + "_" + passwd);
            if (result == "lol")
            {
                IntraThreads.GetInstance().monId = userID;
                ChatWindow chat = new ChatWindow();
                chat.Show();
                Hide();
            }
            else
            {
                dlgFail.IsOpen = true;
            }
        }
    }
}
