using System.Collections.Generic;

namespace DT_Tools.Game.WebTablet
{
    public static class WebTabletKind
    {
        public const string Chat = "chat";
        public const string Proposition = "proposition";
        public const string Photo = "photo";
        public const string State = "state";
    }

    public sealed class WebTabletLine
    {
        public long Seq;
        public int Round;
        public string Kind;
        public string Time;
        public int PlayerId;
        public string Name;
        public string Avatar;
        public bool IsMine;
        public bool IsDead;
        public string Text;
        public int PhotoKey;
    }

    public sealed class WebTabletClue
    {
        public string Key;
        public bool IsProposition;
        public bool IsMine;
        public int OwnerId;
        public string OwnerName;
        public string OwnerAvatar;
        public string Who;
        public string WhoAvatar;
        public string Where;
        public string When;
        public string Sentence;
        public bool Destroyed;
        public int Time;
        public int Round;
        public int TargetId;
        public int RoomId;
    }

    public sealed class WebTabletSnapshot
    {
        public int Epoch;
        public long Seq;
        public string StateCode;
        public string StateLabel;
        public List<WebTabletLine> Lines;
        public List<WebTabletClue> Clues;
    }
}
