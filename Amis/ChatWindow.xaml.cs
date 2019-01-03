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
using MaterialDesignThemes.Wpf;
using MaterialDesignColors;
using System.Net;

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

        private int setIdx = -1;

        public ChatWindow()
        {
            InitializeComponent();

            inter = InterThreads.GetInstance();
            intra = IntraThreads.GetInstance();

            p2pCore = P2PModule.GetInstance();
            csCore = CSModule.GetInstance();
            lock (inter) inter.processing = true;
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
            lblSelfID.Content = "我：" + intra.monId;
            lblSelfAli.Content = intra.monAlias;
            p2pCore.BeginListen();
            threadPump.Start();

            lstAmisSingle.ItemsSource = intra.amisCollection;
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
            MyProto fetches = MyProto.UnpackMessage(msg);
            if(fetches.Type == MessageType.Text && fetches.ToId == intra.monId)
            {
                AddMessage(fetches.Text, true);
            }
        }

        private void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            SendMessage();
        }

        private void SendMessage()
        {
            if (intra.currentChat == -1) return;

            if (tbSender.Text != "")
            {
                AddMessage(tbSender.Text, false);
                MyProto fetches = new MyProto
                {
                    Type = MessageType.Text,
                    Text = tbSender.Text,
                    FromId = intra.monId,
                    ToId = intra.amisIds[intra.currentChat]
                };
                p2pCore.SendData(MyProto.PackMessage(fetches), p2pCore.theIP.ToString(), 15120);
            }

            tbSender.Text = "";
        }

        private void AddMessage(string on, bool recv)
        {
            TextBlock blockMsg = new TextBlock()
            {
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                MaxWidth = 325,
                Padding = new Thickness(10),
                Text = on
            };
            Card cardMsg = new Card()
            {
                Content = blockMsg,
                Margin = new Thickness(10, 5, 10, 5),
                UniformCornerRadius = 5
            };
            cardMsg.SetResourceReference(BackgroundProperty, "PrimaryHueMidBrush");
            cardMsg.SetResourceReference(ForegroundProperty, "PrimaryHueMidForegroundBrush");
            ListBoxItem itemMsg = new ListBoxItem()
            {
                Content = cardMsg,
                HorizontalAlignment = recv ? HorizontalAlignment.Left : HorizontalAlignment.Right,
                MinWidth = 15
            };
            lbMessage.Items.Add(itemMsg);
        }
        

        private void BtnAddAmis_Click(object sender, RoutedEventArgs e)
        {
            dlgAdd.IsOpen = true;
            spNewSingle.Visibility = Visibility.Visible;
            spSetAlias.Visibility = Visibility.Collapsed;
        }

        private void BtnFindAmis_Click(object sender, RoutedEventArgs e)
        {
            FindAmis();
        }

        void FindAmis()
        {
            string idToFind = tbFindAmis.Text;
            if (intra.amisIds.Contains(idToFind))
            {
                lblFindSingleRes.Content = "您已经添加了";
                return;
            }
            /*if (idToFind == intra.monId)
            {
                lblFindSingleRes.Content = "请不要添加自己";
                return;
            }*/
            string result = csCore.QueryOnce("q" + idToFind);
            bool validIP = true;
            try
            {
                IPAddress.Parse(result);
            }
            catch
            {
                validIP = false;
            }
            if (validIP)
            {
                intra.amisIds.Add(idToFind);
                MonAmis noveau = new MonAmis(idToFind)
                {
                    ID = idToFind,
                    LastActivated = DateTime.Now.ToShortTimeString(),
                    Online = true,
                    LastIP = result
                };
                intra.amisCollection.Add(noveau);
                tbFindAmis.Text = "";
                lblFindSingleRes.Content = "";
                dlgAdd.IsOpen = false;
            }
            else if (result == "n")
            {
                lblFindSingleRes.Content = "用户不在线";
            }
            else
            {
                lblFindSingleRes.Content = "查询错误";
            }
        }

        private void BtnExitFindAmis_Click(object sender, RoutedEventArgs e)
        {
            tbFindAmis.Text = "";
            lblFindSingleRes.Content = "";
        }

        private void BtnAccAli_Click(object sender, RoutedEventArgs e)
        {
            SetAlias(setIdx, tbAlias.Text);
            tbAlias.Text = "";
        }

        private void BtnCanAli_Click(object sender, RoutedEventArgs e)
        {
            tbAlias.Text = "";
        }

        private void LbiSelf_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            setIdx = -1;
            dlgAdd.IsOpen = true;
            spNewSingle.Visibility = Visibility.Collapsed;
            spSetAlias.Visibility = Visibility.Visible;
        }

        private void SetAlias(int idx, string on)
        {
            if (idx == -1)
            {
                intra.monAlias = on;
                lblSelfAli.Content = on;
            }
            else
            {
                intra.amisCollection[idx].Alias = on;
                lstAmisSingle.Items.Refresh();
                UpdateChatTitle(idx);
            }
            setIdx = -1;
        }

        private void TbAlias_KeyDown(object sender, KeyEventArgs e)
        {
            if(e.Key == Key.Enter)
            {
                SetAlias(setIdx, tbAlias.Text);
                tbAlias.Text = "";
                dlgAdd.IsOpen = false;
            }
        }

        private void TbFindAmis_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                FindAmis();
            }
        }

        private void LstAmisSingle_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int selAmis = lstAmisSingle.SelectedIndex;
            intra.currentChat = selAmis;
            UpdateChatTitle(selAmis);
        }

        private void UpdateChatTitle(int idx)
        {
            MonAmis amisChat = intra.amisCollection[idx];
            string chatTitle = amisChat.Alias == "未设置备注" ? amisChat.ID : amisChat.Alias;
            lblChat.Content = chatTitle;
        }

        private void LstAmisSingle_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            setIdx = lstAmisSingle.SelectedIndex;
            dlgAdd.IsOpen = true;
            spNewSingle.Visibility = Visibility.Collapsed;
            spSetAlias.Visibility = Visibility.Visible;
        }

        private void TbSender_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                SendMessage();
            }
        }
    }
}
