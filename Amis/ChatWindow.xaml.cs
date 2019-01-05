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
using System.Globalization;
using System.IO;
using Microsoft.Win32;
using System.Collections.ObjectModel;

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
        private const int fileBufferSize = 15 * 1024 * 1024;
        private const int maxPicLength = 1500;
        private byte[] fileBuffer = null;
        private int setIdx = -1;
        private string folderName = null;
        private string cacheFolderName = null;
        private bool closing = false;

        public ChatWindow()
        {
            InitializeComponent();

            inter = InterThreads.GetInstance();
            intra = IntraThreads.GetInstance();

            p2pCore = P2PModule.GetInstance();
            csCore = CSModule.GetInstance();

            fileBuffer = new byte[fileBufferSize];

            lock (inter) inter.processing = true;
            threadPump = new Thread(Relay)
            {
                Name = "MyMessagePump"
            };
        }

        private void AmisChat_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            closing = true;
            p2pCore.EndListen();
            lock (inter)
            {
                inter.processing = false;
            }
            threadPump.Join();
            string res = csCore.QueryOnce("logout" + intra.monId);
            intra.SaveAmis(folderName);
            // TODO:Saving ...
            Application.Current.MainWindow.Show();
        }

        private void LbiQuit_Selected(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void AmisChat_Loaded(object sender, RoutedEventArgs e)
        {
            folderName = "./" + intra.monId + "/";
            cacheFolderName = folderName + "Cache/";

            // TODO:Loading data...
            lblSelfID.Content = "我：" + intra.monId;
            lblSelfAli.Content = intra.monAlias;
            if (!Directory.Exists(folderName)) Directory.CreateDirectory(folderName);
            
            if (!Directory.Exists(cacheFolderName)) Directory.CreateDirectory(cacheFolderName);

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
            while (ongoing)
            {
                byte[] msg = null;
                lock (inter)
                {
                    if (inter.messages.Count > 0)
                    {
                        msg = inter.messages.Dequeue();
                    }
                }
                if (msg != null && msg.Length > 0)
                {
                    Dispatcher.BeginInvoke(new LogicProc(RecvDispatch), msg);
                }
                lock (inter)
                {
                    ongoing = inter.processing;
                }
            }
        }

        private delegate void LogicProc(byte[] msg);

        private async void RecvDispatch(byte[] msg)
        {
            MyProto fetches = MyProto.UnpackMessage(msg);
            if (fetches.ToId != intra.monId) return;
            string timeUpd = null;
            switch (fetches.Type)
            {
                case MessageType.Text:
                    Piece textPiece = new Piece()
                    {
                        MsgType = PieceType.Text,
                        Content = fetches.Text,
                        DstID = fetches.ToId,
                        SrcID = fetches.FromId,
                        Timestamp = DateTime.Now.ToShortTimeString(),
                        HorizAlgn = HorizontalAlignment.Left
                    };
                    intra.history[fetches.FromId].Add(textPiece);
                    timeUpd = textPiece.Timestamp;
                    break;
                case MessageType.File:
                case MessageType.Pic:
                case MessageType.Exp:
                    string filename = cacheFolderName + fetches.Text;
                    FileStream fs = File.OpenWrite(filename);
                    Piece filePiece = new Piece()
                    {
                        DstID = fetches.ToId,
                        SrcID = fetches.FromId,
                        Content = fetches.Text,
                        FilePath = System.IO.Path.GetFullPath(filename),
                        HorizAlgn = HorizontalAlignment.Left,
                        Timestamp = DateTime.Now.ToShortTimeString()
                    };
                    if (fetches.Type == MessageType.File)
                    {
                        filePiece.MsgType = PieceType.File;
                    }
                    else if (fetches.Type == MessageType.Pic)
                    {
                        filePiece.MsgType = PieceType.Image;
                    }
                    else if(fetches.Type == MessageType.Exp)
                    {
                        filePiece.MsgType = PieceType.DynExp;
                    }
                    await fs.WriteAsync(fetches.FilePart, 0, fetches.FilePart.Length);
                    fs.Close();
                    intra.history[fetches.FromId].Add(filePiece);
                    timeUpd = filePiece.Timestamp;
                    break;
            }
            intra.amisCollection[intra.currentChat].LastActivated = timeUpd;
            lstAmisSingle.Items.Refresh();

        }

        private void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            SendMessage();
        }

        private void SendMessage()
        {
            if (intra.currentChat == -1) return;
            string olCheck = intra.amisIds[intra.currentChat];
            string result = csCore.QueryOnce("q" + olCheck);
            try
            {
                IPAddress.Parse(result);
                intra.amisCollection[intra.currentChat].LastIP = result;
            }
            catch
            {
                lblNotif.Content = "朋友当前离线";
                dlgAdd.IsOpen = true;
                spNotif.Visibility = Visibility.Visible;
                spSetAlias.Visibility = Visibility.Collapsed;
                spNewSingle.Visibility = Visibility.Collapsed;
                return;
            }
            if (tbSender.Text != "")
            {
                MyProto fetches = new MyProto
                {
                    Type = MessageType.Text,
                    Text = tbSender.Text,
                    FromId = intra.monId,
                    ToId = intra.amisIds[intra.currentChat]
                };
                Piece pack = new Piece()
                {
                    MsgType = PieceType.Text,
                    Content = fetches.Text,
                    DstID = fetches.ToId,
                    SrcID = fetches.FromId,
                    Timestamp = DateTime.Now.ToShortTimeString(),
                    HorizAlgn = HorizontalAlignment.Right
                };
                intra.history[fetches.ToId].Add(pack);
                p2pCore.SendData(MyProto.PackMessage(fetches), intra.amisCollection[intra.currentChat].LastIP, 15120);
            }

            tbSender.Text = "";
        }

        private void BtnAddAmis_Click(object sender, RoutedEventArgs e)
        {
            dlgAdd.IsOpen = true;
            spNewSingle.Visibility = Visibility.Visible;
            spSetAlias.Visibility = Visibility.Collapsed;
            spNotif.Visibility = Visibility.Collapsed;
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
                intra.history.Add(idToFind, new ObservableCollection<Piece>());
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
            spNotif.Visibility = Visibility.Collapsed;
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
                intra.amisCollection[idx].Alias = on == "" ? "未设置备注" : on;
                lstAmisSingle.Items.Refresh();
                UpdateChatTitle(idx);
            }
            setIdx = -1;
        }

        private void TbAlias_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
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
            if (closing) return;
            int selAmis = lstAmisSingle.SelectedIndex;
            intra.currentChat = selAmis;
            UpdateChatTitle(selAmis);
            string idSel = intra.amisIds[selAmis];
            lbMessage.ItemsSource = intra.history[idSel];
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
            spNotif.Visibility = Visibility.Collapsed;
        }

        private void TbSender_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                SendMessage();
            }
        }

        private void LblChat_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            setIdx = lstAmisSingle.SelectedIndex;
            dlgAdd.IsOpen = true;
            spNewSingle.Visibility = Visibility.Collapsed;
            spSetAlias.Visibility = Visibility.Visible;
            spNotif.Visibility = Visibility.Collapsed;
        }

        private async void BtnSendFile_Click(object sender, RoutedEventArgs e)
        {
            if (intra.currentChat == -1) return;
            string olCheck = intra.amisIds[intra.currentChat];
            string result = csCore.QueryOnce("q" + olCheck);
            try
            {
                IPAddress.Parse(result);
                intra.amisCollection[intra.currentChat].LastIP = result;
            }
            catch
            {
                lblNotif.Content = "朋友当前离线";
                dlgAdd.IsOpen = true;
                spNotif.Visibility = Visibility.Visible;
                spSetAlias.Visibility = Visibility.Collapsed;
                spNewSingle.Visibility = Visibility.Collapsed;
                return;
            }
            OpenFileDialog dlgFileTransmit = new OpenFileDialog
            {
                Title = "选择要发送的文件",
                Multiselect = false,
                DereferenceLinks = true
            };
            if (dlgFileTransmit.ShowDialog() == true)
            {
                Array.Clear(fileBuffer, 0, fileBuffer.Length);
                string filename = dlgFileTransmit.FileName;
                FileInfo info = new FileInfo(filename);
                if (info.Length > fileBufferSize)
                {
                    lblNotif.Content = "不支持过大的文件";
                    dlgAdd.IsOpen = true;
                    spNotif.Visibility = Visibility.Visible;
                    spSetAlias.Visibility = Visibility.Collapsed;
                    spNewSingle.Visibility = Visibility.Collapsed;
                    return;
                }
                string shortFileName = System.IO.Path.GetFileName(filename);
                Piece piece = new Piece()
                {
                    MsgType = PieceType.File,
                    FilePath = System.IO.Path.GetFullPath(filename),
                    SrcID = intra.monId,
                    DstID = intra.amisIds[intra.currentChat],
                    Timestamp = DateTime.Now.ToShortTimeString(),
                    Content = shortFileName,
                    HorizAlgn = HorizontalAlignment.Right
                };
                intra.history[piece.DstID].Add(piece);
                FileStream fs = File.OpenRead(filename);
                int fileLength = await fs.ReadAsync(fileBuffer, 0, fileBuffer.Length);
                MyProto fetches = new MyProto()
                {
                    FromId = piece.SrcID,
                    ToId = piece.DstID,
                    Type = MessageType.File,
                    Text = shortFileName,
                    FilePart = new byte[fileLength]
                };
                Buffer.BlockCopy(fileBuffer, 0, fetches.FilePart, 0, fileLength);
                p2pCore.SendData(MyProto.PackMessage(fetches), intra.amisCollection[intra.currentChat].LastIP, 15120);

            }
        }

        private async void BtnImg_Click(object sender, RoutedEventArgs e)
        {
            if (intra.currentChat == -1) return;
            string olCheck = intra.amisIds[intra.currentChat];
            string result = csCore.QueryOnce("q" + olCheck);
            try
            {
                IPAddress.Parse(result);
                intra.amisCollection[intra.currentChat].LastIP = result;
            }
            catch
            {
                lblNotif.Content = "朋友当前离线";
                dlgAdd.IsOpen = true;
                spNotif.Visibility = Visibility.Visible;
                spSetAlias.Visibility = Visibility.Collapsed;
                spNewSingle.Visibility = Visibility.Collapsed;
                return;
            }
            OpenFileDialog dlgFileTransmit = new OpenFileDialog
            {
                Title = "选择要发送的图片",
                Multiselect = false,
                DereferenceLinks = true,
                Filter = "位图 (.bmp)|*.bmp|联合图像专家组 (.jpg)|*.jpg|便携式网络图形 (.png)|*.png",
                FilterIndex = 0
            };
            if (dlgFileTransmit.ShowDialog() == true)
            {
                Array.Clear(fileBuffer, 0, fileBuffer.Length);
                string filename = dlgFileTransmit.FileName;
                FileInfo info = new FileInfo(filename);
                if (info.Length > fileBufferSize)
                {
                    lblNotif.Content = "不支持过大的图片";
                    dlgAdd.IsOpen = true;
                    spNotif.Visibility = Visibility.Visible;
                    spSetAlias.Visibility = Visibility.Collapsed;
                    spNewSingle.Visibility = Visibility.Collapsed;
                    return;
                }
                string shortFileName = System.IO.Path.GetFileName(filename);
                Piece piece = new Piece()
                {
                    MsgType = PieceType.Image,
                    FilePath = System.IO.Path.GetFullPath(filename),
                    SrcID = intra.monId,
                    DstID = intra.amisIds[intra.currentChat],
                    Timestamp = DateTime.Now.ToShortTimeString(),
                    Content = shortFileName,
                    HorizAlgn = HorizontalAlignment.Right
                };
                intra.history[piece.DstID].Add(piece);
                FileStream fs = File.OpenRead(filename);
                int fileLength = await fs.ReadAsync(fileBuffer, 0, fileBuffer.Length);
                MyProto fetches = new MyProto()
                {
                    FromId = piece.SrcID,
                    ToId = piece.DstID,
                    Type = MessageType.Pic,
                    Text = shortFileName,
                    FilePart = new byte[fileLength]
                };
                Buffer.BlockCopy(fileBuffer, 0, fetches.FilePart, 0, fileLength);
                p2pCore.SendData(MyProto.PackMessage(fetches), intra.amisCollection[intra.currentChat].LastIP, 15120);
            }
        }

        private async void BtnSticker_Click(object sender, RoutedEventArgs e)
        {
            if (intra.currentChat == -1) return;
            string olCheck = intra.amisIds[intra.currentChat];
            string result = csCore.QueryOnce("q" + olCheck);
            try
            {
                IPAddress.Parse(result);
                intra.amisCollection[intra.currentChat].LastIP = result;
            }
            catch
            {
                lblNotif.Content = "朋友当前离线";
                dlgAdd.IsOpen = true;
                spNotif.Visibility = Visibility.Visible;
                spSetAlias.Visibility = Visibility.Collapsed;
                spNewSingle.Visibility = Visibility.Collapsed;
                return;
            }
            OpenFileDialog dlgFileTransmit = new OpenFileDialog
            {
                Title = "请选择要发送的动态表情",
                Multiselect = false,
                DereferenceLinks = true,
                Filter = "图像互换格式 (.gif)|*.gif",
                FilterIndex = 0
            };
            if (dlgFileTransmit.ShowDialog() == true)
            {
                Array.Clear(fileBuffer, 0, fileBuffer.Length);
                string filename = dlgFileTransmit.FileName;
                FileInfo info = new FileInfo(filename);
                if (info.Length > fileBufferSize)
                {
                    lblNotif.Content = "不支持过大的图片";
                    dlgAdd.IsOpen = true;
                    spNotif.Visibility = Visibility.Visible;
                    spSetAlias.Visibility = Visibility.Collapsed;
                    spNewSingle.Visibility = Visibility.Collapsed;
                    return;
                }
                string shortFileName = System.IO.Path.GetFileName(filename);
                Piece piece = new Piece()
                {
                    MsgType = PieceType.DynExp,
                    FilePath = System.IO.Path.GetFullPath(filename),
                    SrcID = intra.monId,
                    DstID = intra.amisIds[intra.currentChat],
                    Timestamp = DateTime.Now.ToShortTimeString(),
                    Content = shortFileName,
                    HorizAlgn = HorizontalAlignment.Right
                };
                intra.history[piece.DstID].Add(piece);
                FileStream fs = File.OpenRead(filename);
                int fileLength = await fs.ReadAsync(fileBuffer, 0, fileBuffer.Length);
                MyProto fetches = new MyProto()
                {
                    FromId = piece.SrcID,
                    ToId = piece.DstID,
                    Type = MessageType.Exp,
                    Text = shortFileName,
                    FilePart = new byte[fileLength]
                };
                Buffer.BlockCopy(fileBuffer, 0, fetches.FilePart, 0, fileLength);
                p2pCore.SendData(MyProto.PackMessage(fetches), intra.amisCollection[intra.currentChat].LastIP, 15120);
            }
        }

        private void MediaExp_MediaEnded(object sender, RoutedEventArgs e)
        {
            var self = sender as MediaElement;
            self.Position = self.Position.Add(TimeSpan.FromMilliseconds(1));
        }

        private void AmisChat_Closed(object sender, EventArgs e)
        {
            lstAmisSingle.ItemsSource = null;
            lbMessage.ItemsSource = null;
            intra.Reset();
        }
    }

    public class PieceVisConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Visibility result = Visibility.Collapsed;
            if ((Type)parameter == typeof(TextBlock))
            {
                if ((PieceType)value == PieceType.Text) result = Visibility.Visible;
            }
            if((Type)parameter == typeof(Image))
            {
                if ((PieceType)value == PieceType.Image) result = Visibility.Visible;
            }
            if((Type)parameter == typeof(Label))
            {
                if ((PieceType)value == PieceType.File) result = Visibility.Visible;
            }
            if((Type)parameter == typeof(PackIcon))
            {
                if ((PieceType)value == PieceType.File) result = Visibility.Visible;
            }
            if((Type)parameter == typeof(MediaElement))
            {
                if ((PieceType)value == PieceType.DynExp) result = Visibility.Visible;
            }
            return result;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
