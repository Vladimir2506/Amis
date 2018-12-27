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
            result = connectionToServer.QueryOnce("logout"+userID);
            Console.WriteLine(result);
            connectionToServer.Release();
        }
    }
}
