using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

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

        private void AmisChat_Closing(object sender, CancelEventArgs e)
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
            if (!Directory.Exists(folderName)) Directory.CreateDirectory(folderName);
            if (!Directory.Exists(cacheFolderName)) Directory.CreateDirectory(cacheFolderName);
            intra.LoadAmis(folderName);
            CheckOnline();
            lblSelfID.Content = "我：" + intra.monId;
            lblSelfAli.Content = intra.monAlias;
            
            p2pCore.BeginListen();
            threadPump.Start();

            lstAmisSingle.ItemsSource = intra.amisCollection;
        }

        private void CheckOnline()
        {
            for (int l = 0; l < intra.amisCollection.Count; ++l)
            {
                var a = intra.amisCollection[l];
                string result = csCore.QueryOnce("q" + a.ID);
                try
                {
                    IPAddress.Parse(result);
                    a.LastIP = result;
                    a.Online = true;
                }
                catch
                {
                    a.Online = false;
                }
            }
            lstAmisSingle.Items.Refresh();
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
                    MyProto fetches = MyProto.UnpackMessage(msg);
                    if(fetches.Type != MessageType.Invalid)
                        Dispatcher.BeginInvoke(new MainLogic(RecvDispatch), fetches);
                }
                lock (inter)
                {
                    ongoing = inter.processing;
                }
            }
        }

        private delegate void MainLogic(MyProto fetches);

        private async void RecvDispatch(MyProto fetches)
        {
            if (fetches.ToId != intra.monId) return;
            string timeUpd = null;
            if(!intra.amisIds.Contains(fetches.FromId))
            {
                intra.amisIds.Add(fetches.FromId);
                intra.amisCollection.Add(
                    new MonAmis("")
                    {
                        ID = fetches.FromId,
                        Alias = "未设置备注",
                        LastActivated = DateTime.Now.ToShortTimeString(),
                        LastIP = csCore.QueryOnce("q" + fetches.FromId),
                        Online = true
                    });
                intra.history.Add(fetches.FromId,
                    new ObservableCollection<Piece>());
                lstAmisSingle.Items.Refresh();
                lstAmisSingle.SelectedIndex = lstAmisSingle.Items.Count - 1;
            }
            else
            {
                lstAmisSingle.SelectedIndex = intra.amisIds.FindIndex(x => x == fetches.FromId);
            }
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
                    lbMessage.ScrollIntoView(textPiece);
                    break;
                case MessageType.File:
                case MessageType.Pic:
                case MessageType.Exp:
                    string filename = cacheFolderName + fetches.Text;
                    string ext = Path.GetExtension(filename);
                    string fn = Path.GetFileNameWithoutExtension(filename);
                    filename = cacheFolderName +  fn + "_" + DateTime.Now.ToLongTimeString() + ext;
                    filename = filename.Replace(':', '_');
                    Piece filePiece = new Piece()
                    {
                        DstID = fetches.ToId,
                        SrcID = fetches.FromId,
                        Content = fetches.Text,
                        FilePath = Path.GetFullPath(filename),
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
                    FileStream fs = File.OpenWrite(filePiece.FilePath);
                    await fs.WriteAsync(fetches.FilePart, 0, fetches.FilePart.Length);
                    fs.Close();
                    intra.history[fetches.FromId].Add(filePiece);
                    timeUpd = filePiece.Timestamp;
                    lbMessage.ScrollIntoView(filePiece);
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
                try
                {
                    p2pCore.SendData(MyProto.PackMessage(fetches), intra.amisCollection[intra.currentChat].LastIP, 15120);
                }
                catch (Exception ex)
                {
                    if (ex.Message == "NOL")
                    {
                        lblNotif.Content = "朋友当前离线";
                        dlgAdd.IsOpen = true;
                        intra.amisCollection[intra.currentChat].Online = false;
                        spNotif.Visibility = Visibility.Visible;
                        spSetAlias.Visibility = Visibility.Collapsed;
                        spNewSingle.Visibility = Visibility.Collapsed;
                    }
                }
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
            if (idToFind == intra.monId)
            {
                lblFindSingleRes.Content = "请不要添加自己";
                return;
            }
            if (intra.amisIds.Contains(idToFind))
            {
                tbFindAmis.Text = "";
                dlgAdd.IsOpen = false;
                lstAmisSingle.SelectedIndex = intra.amisIds.FindIndex(x => x == idToFind);
                return;
            }
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
                string shortFileName = Path.GetFileName(filename);
                Piece piece = new Piece()
                {
                    MsgType = PieceType.File,
                    FilePath = Path.GetFullPath(filename),
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
                try
                {
                    p2pCore.SendData(MyProto.PackMessage(fetches), intra.amisCollection[intra.currentChat].LastIP, 15120);
                }
                catch(Exception ex)
                {
                    if(ex.Message == "NOL")
                    {
                        lblNotif.Content = "朋友当前离线";
                        dlgAdd.IsOpen = true;
                        intra.amisCollection[intra.currentChat].Online = false;
                        spNotif.Visibility = Visibility.Visible;
                        spSetAlias.Visibility = Visibility.Collapsed;
                        spNewSingle.Visibility = Visibility.Collapsed;
                    }
                }
            }
        }

        private async void BtnImg_Click(object sender, RoutedEventArgs e)
        {
            if (intra.currentChat == -1) return;
            
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
                string shortFileName = Path.GetFileName(filename);
                Piece piece = new Piece()
                {
                    MsgType = PieceType.Image,
                    FilePath = Path.GetFullPath(filename),
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
                try
                {
                    p2pCore.SendData(MyProto.PackMessage(fetches), intra.amisCollection[intra.currentChat].LastIP, 15120);
                }
                catch (Exception ex)
                {
                    if (ex.Message == "NOL")
                    {
                        lblNotif.Content = "朋友当前离线";
                        dlgAdd.IsOpen = true;
                        intra.amisCollection[intra.currentChat].Online = false;
                        spNotif.Visibility = Visibility.Visible;
                        spSetAlias.Visibility = Visibility.Collapsed;
                        spNewSingle.Visibility = Visibility.Collapsed;
                    }
                }
            }
        }
        

        private async void BtnSticker_Click(object sender, RoutedEventArgs e)
        {
            if (intra.currentChat == -1) return;
            
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
                string shortFileName = Path.GetFileName(filename);
                Piece piece = new Piece()
                {
                    MsgType = PieceType.DynExp,
                    FilePath = Path.GetFullPath(filename),
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
                try
                {
                    p2pCore.SendData(MyProto.PackMessage(fetches), intra.amisCollection[intra.currentChat].LastIP, 15120);
                }
                catch (Exception ex)
                {
                    if (ex.Message == "NOL")
                    {
                        lblNotif.Content = "朋友当前离线";
                        dlgAdd.IsOpen = true;
                        intra.amisCollection[intra.currentChat].Online = false;
                        spNotif.Visibility = Visibility.Visible;
                        spSetAlias.Visibility = Visibility.Collapsed;
                        spNewSingle.Visibility = Visibility.Collapsed;
                    }
                }
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

        private void BtnOLCheck_Click(object sender, RoutedEventArgs e)
        {
            CheckOnline();
            exChat.IsExpanded = true;
        }

        private void CardMessage_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            int idx = lbMessage.SelectedIndex;
            Piece p = lbMessage.SelectedItem as Piece;
            if(p.MsgType == PieceType.File)
            {
                Process.Start(p.FilePath);
            }
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

    public class OnlineConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            
            if ((bool)value) return new SolidColorBrush(Color.FromArgb(0xdd, 0x42, 0xa5, 0xf5));
            else return new SolidColorBrush(Color.FromArgb(0xdd, 0x00, 0x00, 0x00));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
