using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Text;
using System.Windows;

namespace Amis
{
    public class InterThreads
    {
        private static InterThreads instance = null;

        public bool listening = false;
        public bool processing = false;
        public Queue<byte[]> messages = null;

        private InterThreads()
        {
            messages = new Queue<byte[]>();
        }

        public static InterThreads GetInstance()
        {
            if (instance == null)
            {
                instance = new InterThreads();
            }
            return instance;
        }
    }

    public class IntraThreads
    {
        private static IntraThreads instance = null;

        public const int portNO = 15120;
        public const int backlog = 12;
        public string monId = null;
        public string monAlias = null;
        public List<string> amisIds = null;
        public ObservableCollection<MonAmis> amisCollection = null;
        public Dictionary<string, ObservableCollection<Piece>> history = null;
        public int currentChat = -1;
        private const string nameDataBase = "LocalRecords.db3";

        private IntraThreads()
        {
            amisIds = new List<string>();
            amisCollection = new ObservableCollection<MonAmis>();
            history = new Dictionary<string, ObservableCollection<Piece>>();
            monAlias = "未设置备注";
        }

        public static IntraThreads GetInstance()
        {
            if (instance == null)
            {
                instance = new IntraThreads();
            }
            return instance;
        }

        public void LoadAmis(string path)
        {
            if (!File.Exists(path + nameDataBase)) return;
            using (SQLiteConnection dbCore =
                new SQLiteConnection("data source=" + path + nameDataBase))
            {
                dbCore.Open();
                SQLiteCommand cmd = new SQLiteCommand(dbCore);
                string tableName = "AMIS_" + monId;
                cmd.CommandText = "SELECT * FROM " + tableName + ";";
                SQLiteDataReader dataReader = null;
                try
                {
                    dataReader = cmd.ExecuteReader();
                }
                catch
                {
                    return;
                }
                while(dataReader.Read())
                {
                    MonAmis m = new MonAmis("")
                    {
                        ID = dataReader.GetString(0),
                        Alias = dataReader.GetString(1),
                        LastActivated = dataReader.GetString(2),
                        LastIP = dataReader.GetString(3)
                    };
                    if (amisIds.Contains(m.ID)) continue;

                    if (m.ID != monId)
                    {
                        amisCollection.Add(m);
                        amisIds.Add(m.ID);
                    }
                    else monAlias = m.Alias;
                }
                dataReader.Close();
                string nameHist = "HIST_";
                foreach (string id in amisIds)
                {
                    ObservableCollection<Piece> pieces = new ObservableCollection<Piece>();
                    cmd.CommandText = "SELECT * FROM " + nameHist + id + ";";
                    try
                    {
                        dataReader = cmd.ExecuteReader();
                    }
                    catch
                    {
                        history.Add(id, pieces);
                        continue;
                    }
                    while(dataReader.Read())
                    {
                        Piece p = new Piece()
                        {
                            MsgType = (PieceType)dataReader.GetInt32(0),
                            Content = dataReader.GetString(1),
                            SrcID = dataReader.GetString(2),
                            DstID = dataReader.GetString(3),
                            Timestamp = dataReader.GetString(4),
                            HorizAlgn = (HorizontalAlignment)dataReader.GetInt32(5),
                            FilePath = dataReader.GetString(6)
                        };
                        pieces.Add(p);
                    }
                    dataReader.Close();
                    history.Add(id, pieces);
                }
                dbCore.Close();
            }
        }

        public void SaveAmis(string path)
        {
            if (File.Exists(path + nameDataBase)) File.Delete(path + nameDataBase);
                using (SQLiteConnection dbCore =
                new SQLiteConnection("data source=" + path + nameDataBase))
            {
                dbCore.Open();
                SQLiteCommand cmd = new SQLiteCommand(dbCore);
                string tableName = "AMIS_" + monId;
                cmd.CommandText =
                    "CREATE TABLE IF NOT EXISTS "
                    + tableName
                    + "(ID NTEXT NOT NULL,"
                    + "ALIAS NTEXT NOT NULL,"
                    + "LAST NTEXT NOT NULL,"
                    + "IP NTEXT NOT NULL);";
                cmd.ExecuteNonQuery();
                cmd.CommandText =
                    "INSERT OR REPLACE INTO "
                    + tableName
                    + "(ID, ALIAS, LAST, IP) VALUES(@ID, @ALIAS, @LAST, @IP);";
                cmd.Parameters.Add("ID", DbType.String).Value = monId;
                cmd.Parameters.Add("ALIAS", DbType.String).Value = monAlias;
                cmd.Parameters.Add("LAST", DbType.String).Value = "";
                cmd.Parameters.Add("IP", DbType.String).Value = "";
                cmd.ExecuteNonQuery();
                foreach (MonAmis m in amisCollection)
                {
                    cmd.Parameters["ID"].Value = m.ID;
                    cmd.Parameters["ALIAS"].Value = m.Alias;
                    cmd.Parameters["LAST"].Value = m.LastActivated;
                    cmd.Parameters["IP"].Value = m.LastIP;
                    cmd.ExecuteNonQuery();
                }
                cmd.Parameters.Clear();
                string histTbSch = 
                    "CREATE TABLE IF NOT EXISTS "
                    + "{0}"
                    + "(MSGTYPE INT NOT NULL,"
                    + "CONTENT NTEXT NOT NULL,"
                    + "SRCID NTEXT NOT NULL,"
                    + "DSTID NTEXT NOT NULL,"
                    + "TIMESTAMP NTEXT NOT NULL,"
                    + "HORIZALGN INT NOT NULL,"
                    + "FILEPATH NTEXT NOT NULL);";
                string histCmd =
                    "INSERT OR REPLACE INTO "
                    + "{0}"
                    + "(MSGTYPE, CONTENT, SRCID, DSTID, TIMESTAMP, HORIZALGN, FILEPATH) "
                    + "VALUES(@MSGTYPE, @CONTENT, @SRCID, @DSTID, @TIMESTAMP, @HORIZALGN, @FILEPATH);";
                foreach (var p in history)
                {
                    string histTableName = "HIST_" + p.Key;
                    ObservableCollection<Piece> pieces = p.Value;
                    cmd.CommandText = string.Format(histTbSch, histTableName);
                    cmd.ExecuteNonQuery();
                    cmd.CommandText = string.Format(histCmd, histTableName);
                    cmd.Parameters.Add("MSGTYPE", DbType.Int32);
                    cmd.Parameters.Add("CONTENT", DbType.String);
                    cmd.Parameters.Add("SRCID", DbType.String);
                    cmd.Parameters.Add("DSTID", DbType.String);
                    cmd.Parameters.Add("TIMESTAMP", DbType.String);
                    cmd.Parameters.Add("HORIZALGN", DbType.Int32);
                    cmd.Parameters.Add("FILEPATH", DbType.String);
                    foreach (Piece h in pieces)
                    {
                        cmd.Parameters["MSGTYPE"].Value = (int)h.MsgType;
                        cmd.Parameters["CONTENT"].Value = h.Content;
                        cmd.Parameters["SRCID"].Value = h.SrcID;
                        cmd.Parameters["DSTID"].Value = h.DstID;
                        cmd.Parameters["TIMESTAMP"].Value = h.Timestamp;
                        cmd.Parameters["HORIZALGN"].Value = (int)h.HorizAlgn;
                        cmd.Parameters["FILEPATH"].Value = h.FilePath;
                        cmd.ExecuteNonQuery();
                    }
                    cmd.Parameters.Clear();
                }
                dbCore.Close();
            }
        }

        public void Reset()
        {
            monAlias = "未设置备注";
            monId = "";
            amisIds.Clear();
            amisCollection.Clear();
            foreach(var p in history) p.Value.Clear();
            history.Clear();
        }
    }

    public enum PieceType
    {
        Invalid = 0,
        Text = 1,
        Image = 2,
        File = 3,
        DynExp = 4
    }

    public class Piece
    {
        public PieceType MsgType { get; set; }
        public string Content { get; set; }
        public string SrcID { get; set; }
        public string DstID { get; set; }
        public string Timestamp { get; set; }
        public HorizontalAlignment HorizAlgn { get; set; }
        public string FilePath { get; set; }

        public Piece()
        {
            MsgType = PieceType.Invalid;
            Content = "";
            SrcID = "";
            Timestamp = "";
            HorizAlgn = HorizontalAlignment.Stretch;
            FilePath = "";
        }
    }

    public class MonAmis
    {
        public string ID { get; set; }
        public string Alias { get; set; }

        public bool Online { get; set; }
        public string LastActivated { get; set; }
        public string LastIP { get; set; }

        public MonAmis(string id)
        {
            ID = id;
            Alias = "未设置备注";
        }
    }

    public enum MessageType
    {
        Invalid = 0,
        Text = 1,
        File = 2,
        Pic = 3,
        Voice = 4,
        Exp = 5
    }

    public class MyProto
    {
        private const uint markBegStd = 2147483655;
        private const uint markEndStd = 3014986642;

        public string FromId { get; set; }
        public string ToId { get; set; }
        public MessageType Type { get; set; }
        public string Text { get; set; }
        public byte[] FilePart { get; set; }
        public int GroupId { get; set; }
        public int ExpId { get; set; }

        public MyProto()
        {
            Type = MessageType.Invalid;
            FromId = null;
            ToId = null;
            Text = null;
            FilePart = null;
            GroupId = -1;
            ExpId = -1;
        }

        public static byte[] PackMessage(MyProto proto)
        {
            /*
             *  Protocol
             *  Max length = 16MB - 1KB
             *  Min length = 32B
             *  4B = BEG
             *  4B = TYP
             *  4B = GPID
             *  4B = FNLEN
             *  4B = FLEN
             *  4B = FRM
             *  4B = TO
             *  ?B = DATA max = 16MB - 1KB - 32B
             *  4B = END
             */
            int lengthFile = -1;
            int lengthFileName = -1;
            List<byte> msg = new List<byte>();
            bool isFile = false;
            bool isExp = proto.Type == MessageType.Exp;
            if ((uint)proto.Type >= 2 && (uint)proto.Type <= 5)
            {
                // Is File
                lengthFile = proto.FilePart.Length;
                lengthFileName = Encoding.UTF8.GetByteCount(proto.Text);
                isFile = true;
            }
            msg.AddRange(BitConverter.GetBytes(markBegStd));
            msg.AddRange(BitConverter.GetBytes((uint)proto.Type));
            msg.AddRange(BitConverter.GetBytes(proto.GroupId));
            msg.AddRange(BitConverter.GetBytes(lengthFileName));
            msg.AddRange(BitConverter.GetBytes(lengthFile));
            msg.AddRange(BitConverter.GetBytes(Convert.ToUInt32(proto.FromId)));
            msg.AddRange(BitConverter.GetBytes(Convert.ToUInt32(proto.ToId)));
            if (isFile)
            {
                // Is File
                msg.AddRange(Encoding.UTF8.GetBytes(proto.Text));
                msg.AddRange(proto.FilePart);
            }
            else
            {
                // Is text
                msg.AddRange(Encoding.UTF8.GetBytes(proto.Text));
            }
            msg.AddRange(BitConverter.GetBytes(markEndStd));
            return msg.ToArray();
        }

        public static MyProto UnpackMessage(byte[] msg)
        {
            /*
             *  Protocol
             *  Max length = 16MB - 1KB
             *  Min length = 32B
             *  4B = BEG
             *  4B = TYP
             *  4B = GPID
             *  4B = FNLEN
             *  4B = FLEN
             *  4B = FRM
             *  4B = TO
             *  ?B = DATA max = 16MB - 1KB - 32B
             *  4B = END
             */
            int lengthMsg = msg.Length;
            MyProto result = new MyProto();
            if (BitConverter.ToUInt32(msg, 0) != markBegStd || 
                BitConverter.ToUInt32(msg, lengthMsg - 4) != markEndStd)
                return result;
            uint type = BitConverter.ToUInt32(msg, 4);
            result.Type = (MessageType)type;
            result.GroupId = BitConverter.ToInt32(msg, 8);
            int lenF = BitConverter.ToInt32(msg, 16);
            int lenFN = BitConverter.ToInt32(msg, 12);
            result.FromId = BitConverter.ToUInt32(msg, 20).ToString();
            result.ToId = BitConverter.ToUInt32(msg, 24).ToString();
            switch(type)
            {
                case 2:
                case 3:
                case 4:
                case 5:
                    byte[] fnPart = new byte[lenFN], fPart = new byte[lenF];
                    Array.Copy(msg, 28, fnPart, 0, lenFN);
                    Array.Copy(msg, 28 + lenFN, fPart, 0, lenF);
                    result.Text = Encoding.UTF8.GetString(fnPart);
                    result.FilePart = fPart;
                    break;
                case 1:
                    result.Text = Encoding.UTF8.GetString(msg, 28, lengthMsg - 32);
                    break;
            }
            return result;
        }
    }
}
