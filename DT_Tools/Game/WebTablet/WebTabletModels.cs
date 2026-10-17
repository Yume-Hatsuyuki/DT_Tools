using System.Collections.Generic;

namespace DT_Tools.Game.WebTablet
{
    /// <summary>
    /// 平板电脑条目类型。与 WebUI 的 kind 字段一一对应，线上只传字符串。
    /// </summary>
    public static class WebTabletKind
    {
        /// <summary>普通聊天消息。</summary>
        public const string Chat = "chat";
        /// <summary>命题气泡（某位玩家提交的命题句）。</summary>
        public const string Proposition = "proposition";
        /// <summary>照片。</summary>
        public const string Photo = "photo";
        /// <summary>阶段切换标记。</summary>
        public const string State = "state";
    }

    /// <summary>
    /// 聊天流中的一条：普通消息、命题气泡、照片或阶段切换标记。
    /// </summary>
    /// <remarks>
    /// 创建后不再修改；Seq 只由 WebTabletStore 在加锁时赋值。
    /// Text 必须已去除富文本标签（命题句带 &lt;color&gt; 标签，见 WebTabletText.StripRichText）。
    /// </remarks>
    public sealed class WebTabletLine
    {
        /// <summary>单调递增序号，由存储分配，前端用于增量拉取。</summary>
        public long Seq;
        /// <summary>所属局号（由存储在入库时写入），与线索的局号共用同一计数。</summary>
        public int Round;
        /// <summary>条目类型，取值见 WebTabletKind。</summary>
        public string Kind;
        /// <summary>接收时的本机时间（HH:mm:ss），只用于导出。</summary>
        public string Time;
        /// <summary>发言玩家编号；阶段标记与照片为发言者编号。</summary>
        public int PlayerId;
        /// <summary>发言人显示名。</summary>
        public string Name;
        /// <summary>头像键，见 WebTabletAvatar。</summary>
        public string Avatar;
        /// <summary>是否为本机发出，决定左右位置。</summary>
        public bool IsMine;
        /// <summary>发言者是否已死亡，决定气泡为灰色。</summary>
        public bool IsDead;
        /// <summary>正文（已去富文本）；阶段标记为「进入「…」」文案。</summary>
        public string Text;
        /// <summary>照片条目的存储键，见 WebTabletStore.GetPhoto。</summary>
        public int PhotoKey;
    }

    /// <summary>
    /// 线索栏中的一条：自己收集的线索，或某位玩家提交的命题。
    /// </summary>
    /// <remarks>
    /// 文本在接收时（主线程）按 0.1.18c UI_ClueSubItem.FillText 同源规则生成，之后不随游戏状态改变。
    /// </remarks>
    public sealed class WebTabletClue
    {
        /// <summary>去重键。同一线索（同拥有者、对象、房间、时间）只入库一次。</summary>
        public string Key;
        /// <summary>是否为命题（提交给全体的句子）。</summary>
        public bool IsProposition;
        /// <summary>拥有者是否为本机。</summary>
        public bool IsMine;
        /// <summary>线索拥有者（收集者或命题提交者）的玩家编号。</summary>
        public int OwnerId;
        /// <summary>拥有者显示名。</summary>
        public string OwnerName;
        /// <summary>拥有者头像键。</summary>
        public string OwnerAvatar;
        /// <summary>线索对象（谁）的显示名。</summary>
        public string Who;
        /// <summary>线索对象的头像键。</summary>
        public string WhoAvatar;
        /// <summary>地点的中文名。</summary>
        public string Where;
        /// <summary>时间文本，形如 "09:30 am"。</summary>
        public string When;
        /// <summary>命题句，仅 IsProposition 时有值。</summary>
        public string Sentence;
        /// <summary>线索是否已被销毁（游戏内的 Destroyed 标记）。</summary>
        public bool Destroyed;
        /// <summary>游戏内时间（分钟），前端用于排序。</summary>
        public int Time;
        /// <summary>所属局号（由存储在入库时写入）。同一局的线索共用一个值，回到大厅即递增。</summary>
        public int Round;
        /// <summary>线索对象的玩家编号（游戏 Info.PlayerId），用于同一时间的次序。</summary>
        public int TargetId;
        /// <summary>线索所在房间的编号（游戏 Info.RoomId），用于同一时间的次序。</summary>
        public int RoomId;
    }

    /// <summary>
    /// 一次拉取的快照：版本信息、阶段、聊天增量与线索全表。
    /// </summary>
    public sealed class WebTabletSnapshot
    {
        /// <summary>存储代数，每次 Clear 递增；代数变化时前端应丢弃旧列表。</summary>
        public int Epoch;
        /// <summary>快照时刻的最大序号，前端下次以此作为 since。</summary>
        public long Seq;
        /// <summary>当前阶段的 EGameState 枚举名，尚未同步时为空串。</summary>
        public string StateCode;
        /// <summary>当前阶段的中文名。</summary>
        public string StateLabel;
        /// <summary>序号大于 since 的聊天条目，按序号升序。</summary>
        public List<WebTabletLine> Lines;
        /// <summary>全部线索，按入库顺序。</summary>
        public List<WebTabletClue> Clues;
    }
}
