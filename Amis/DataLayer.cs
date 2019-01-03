using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Data;
using System.Data.SQLite;
using System.Collections.ObjectModel;

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
        public Dictionary<string, List<Piece>> history = null;

        private IntraThreads()
        {
            amisIds = new List<string>();
            amisCollection = new ObservableCollection<MonAmis>();
            history = new Dictionary<string, List<Piece>>();
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
        
    }

    public enum PieceType { Text, Image, File }

    public class Piece
    {
        public PieceType type;
        public string content = null;
        public string srcID = null;
        public string dstID = null;
        public string timestamp = null;

        public bool lazyDel = false;

        public Piece(PieceType pt, string src, string dst, string con, string ts)
        {
            type = pt;
            srcID = src;
            dstID = dst;
            content = con;
            timestamp = ts;
        }
    }

    public class MonAmis
    {
        public string ID { get; set; }
        public string alias { get; set; }

        public bool online { get; set; }
        public string lastActivated { get; set; }
        public string lastIP { get; set; }

        public MonAmis(string id)
        {
            ID = id;
            alias = "未设置备注";
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
            if ((uint)proto.Type >= 2 && (uint)proto.Type <= 4)
            {
                // Is File
                lengthFile = proto.FilePart.Length;
                lengthFileName = proto.Text.Length;
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
            else if(isExp)
            {
                // Is expression
                msg.AddRange(BitConverter.GetBytes(proto.ExpId));
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
                    byte[] fnPart = new byte[lenFN], fPart = new byte[lenF];
                    Array.Copy(msg, 28, fnPart, 0, lenFN);
                    Array.Copy(msg, 28 + lenFN, fPart, 0, lenF);
                    result.Text = Encoding.UTF8.GetString(fnPart);
                    result.FilePart = fPart;
                    break;
                case 1:
                    result.Text = Encoding.UTF8.GetString(msg, 28, lengthMsg - 32);
                    break;
                case 5:
                    result.ExpId = BitConverter.ToInt32(msg, 28);
                    break;
            }
            return result;
        }
    }
}
