using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Data;
using System.Data.SQLite;

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

        public string monId = null;
        public string monAlias = null;
        public List<MonAmis> amis = null;
        public Dictionary<string, List<Piece>> history = null;

        private IntraThreads()
        {

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
        public string ID = null;
        public string alias = null;

        public bool online = false;

        public MonAmis(string id, string ali)
        {
            ID = id;
            alias = ali;
        }
    }
}
