using System;
using System.Threading;
using System.Windows;

namespace Amis
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        private Mutex mutex;

        public App()
        {
            Startup += App_Startup;
        }

        void App_Startup(object sender, StartupEventArgs e)
        {
            mutex = new Mutex(true, "TheAmis", out bool ret);

            if (!ret)
            {
                MessageBox.Show("已有一个程序实例运行");
                Environment.Exit(0);
            }

        }
    }
}
