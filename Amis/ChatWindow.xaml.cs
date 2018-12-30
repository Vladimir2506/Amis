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

namespace Amis
{
    /// <summary>
    /// ChatWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ChatWindow : Window
    {
        private InterThreads inter = null;
        private IntraThreads intra = null;

        public ChatWindow()
        {
            InitializeComponent();
            inter = InterThreads.GetInstance();
            intra = IntraThreads.GetInstance();
        }

        private void AmisChat_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        { 
            Application.Current.MainWindow.Show();
        }

        private void LbiQuit_Selected(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void AmisChat_Loaded(object sender, RoutedEventArgs e)
        {
            lbiSelf.Content = "我：" + intra.monId;
        }

        private void CzTop_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }
    }
}
