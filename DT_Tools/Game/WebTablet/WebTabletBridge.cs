using System;
using System.Globalization;
using System.Reflection;
using Data;
using Google.Protobuf;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Game.WebTablet
{
    /// <summary>
    /// 平板电脑的游戏侧行为：把游戏事件转成存储条目，并提供经主线程发出的发言与照片。
    /// </summary>
    /// <remarks>
    /// Record* 与 SyncState 只能在 Unity 主线程调用（Harmony 补丁）。
    /// Send* 必须经 WebConsole.RunOnMain 调用。所有文本在此按游戏同源规则生成，HTTP 线程只读存储。
    /// 文本规则与 0.1.18c UI_ClueSubItem.cs 的 GetWhoText（:274）、FormatWhen（:310）保持一致。
    /// </remarks>
    public static class WebTabletBridge
    {
        /// <summary>
        /// 「Black」对应的虚拟玩家编号（0.1.18c UI_ClueSubItem.cs:232 与 Util.cs:804 同源常量）。
        /// </summary>
        private const int BlackClueId = 66613;

        /// <summary>
        /// 照片节流预算的恢复速率（每毫秒）：0.1.18c PhotoManager.cs:110 的 num * 0.2f，每秒回 0.2 格，即 5 秒一张。
        /// 服务端 GameRoom.TryConsumePhotoBudget 使用同一参数，超出时服务端静默丢弃照片。
        /// </summary>
        private const float PhotoRefillPerMs = 0.2f / 1000f;

        /// <summary>
        /// PhotoManager.ConsumeBudget（private，字符串定位）：0.1.18c PhotoManager.cs:715-719。
        /// 平板发图与游戏内发图共用这一份客户端预算，发送后经此方法扣减，保证两条路径的冷却一致。
        /// </summary>
        private static readonly MethodInfo ConsumePhotoBudgetMethod = AccessTools.Method(typeof(PhotoManager), "ConsumeBudget");

        /// <summary>
        /// 记录一条普通聊天（S_CHAT_MESSAGE 的 NormalChat 分支，经 VoiceManager.EnqueueNormalChat 进入）。
        /// </summary>
        /// <param name="playerId">发言玩家编号。</param>
        /// <param name="message">已中和富文本的消息正文。</param>
        /// <param name="isDeadByHost">主机标记的死亡状态。</param>
        public static void RecordNormalChat(int playerId, string message, bool isDeadByHost)
        {
            Player player = Managers.Player?.GetPlayerCache(playerId);
            bool dead = isDeadByHost || (Managers.Player != null && Managers.Player.KnownDeadIds.Contains(playerId));
            WebTabletStore.AddLine(new WebTabletLine
            {
                Kind = WebTabletKind.Chat,
                Time = NowStamp(),
                PlayerId = playerId,
                Name = NameOf(player),
                Avatar = WebTabletAvatar.OfPlayer(player),
                IsMine = IsMine(playerId),
                IsDead = dead,
                Text = message ?? "",
            });
        }

        /// <summary>
        /// 记录一条命题气泡（UI_GameTablet.AddPropositionMessage）。同一命题只入库一次。
        /// </summary>
        /// <param name="playerId">提交命题的玩家编号。</param>
        /// <param name="name">游戏传入的显示名，为空时回退到玩家缓存。</param>
        /// <param name="sentence">游戏生成的命题句，含富文本标签。</param>
        /// <param name="info">命题对应的线索信息。</param>
        public static void RecordProposition(int playerId, string name, string sentence, PropositionInfo info)
        {
            Player player = Managers.Player?.GetPlayerCache(playerId);
            WebTabletStore.AddLine(new WebTabletLine
            {
                Kind = WebTabletKind.Proposition,
                Time = NowStamp(),
                PlayerId = playerId,
                Name = string.IsNullOrEmpty(name) ? NameOf(player) : name,
                Avatar = WebTabletAvatar.OfPlayer(player),
                IsMine = IsMine(playerId),
                IsDead = false,
                Text = WebTabletText.StripRichText(sentence),
            }, $"P:{playerId}:{info.PlayerId}:{info.RoomId}:{info.Time}");
        }

        /// <summary>
        /// 记录一条线索或命题到线索栏（UI_GameTablet.AddClue）。文本按 FillText 同源规则生成。
        /// </summary>
        /// <param name="ownerId">线索的拥有者（自己收集时为本机，命题时为提交者）。</param>
        /// <param name="info">线索信息。</param>
        /// <param name="isProposition">是否为命题。</param>
        public static void RecordClue(int ownerId, PropositionInfo info, bool isProposition)
        {
            Player owner = Managers.Player?.GetPlayerCache(ownerId);
            string sentence = isProposition ? PropositionSentence(ownerId, info) : null;
            WebTabletStore.AddClue(new WebTabletClue
            {
                Key = $"{(isProposition ? "P" : "C")}:{ownerId}:{info.PlayerId}:{info.RoomId}:{info.Time}",
                IsProposition = isProposition,
                IsMine = IsMine(ownerId),
                OwnerId = ownerId,
                OwnerName = NameOf(owner),
                OwnerAvatar = WebTabletAvatar.OfPlayer(owner),
                Who = WhoText(info.PlayerId),
                WhoAvatar = WhoAvatar(info.PlayerId),
                Where = WhereText(info.RoomId),
                When = WhenText(info.Time),
                Sentence = sentence,
                Destroyed = info.Destroyed,
                Time = info.Time,
                TargetId = info.PlayerId,
                RoomId = info.RoomId,
            });
        }

        /// <summary>
        /// 记录一张照片（UI_GameTablet.AddPhotoMessage）。照片字节由存储保留，条目只保存键。
        /// </summary>
        /// <param name="shot">游戏照片对象，Jpeg 为空时忽略。</param>
        public static void RecordPhoto(PhotoManager.PhotoShot shot)
        {
            if (shot == null || shot.Jpeg == null || shot.Jpeg.Length == 0) return;
            Player player = Managers.Player?.GetPlayerCache(shot.PlayerId);
            int key = WebTabletStore.AddPhoto(shot.Jpeg);
            WebTabletStore.AddLine(new WebTabletLine
            {
                Kind = WebTabletKind.Photo,
                Time = NowStamp(),
                PlayerId = shot.PlayerId,
                Name = NameOf(player),
                Avatar = WebTabletAvatar.OfPlayer(player),
                IsMine = IsMine(shot.PlayerId),
                IsDead = shot.IsDead,
                PhotoKey = key,
            });
        }

        /// <summary>
        /// 记录阶段切换（GameManagerEX.State 的 setter）。同一阶段重复上报会被存储忽略。
        /// </summary>
        /// <param name="state">新阶段。</param>
        public static void RecordStateChange(EGameState state)
            => WebTabletStore.SetState(state.ToString(), StateLabel(state), NowStamp());

        /// <summary>
        /// 把当前游戏阶段同步进存储。启用功能时阶段可能早已进入，靠此方法补齐。
        /// </summary>
        public static void SyncState() => RecordStateChange(CurrentState());

        /// <summary>
        /// 发送一条聊天。只有大厅与学级裁判允许发送。
        /// </summary>
        /// <param name="text">已去首尾空白的正文，调用方保证非空。</param>
        /// <returns>null 表示已发出；否则为拒绝原因。</returns>
        public static string SendChat(string text)
        {
            EGameState state = CurrentState();
            string block = WebTabletText.TypeBlockReason(state.ToString(), StateLabel(state));
            if (block != null) return block;
            if (Managers.Voice == null) return "语音系统尚未就绪";
            Managers.Voice.SendChatMessage(text);
            return null;
        }

        /// <summary>
        /// 在学级裁判的讨论阶段发送一张照片。走游戏原有的 C_CHAT_PHOTO 协议，
        /// SlotIndex 取 -1，不占用本机相册槽位（客户端 PhotoManager.Receive 只在 SlotIndex >= 0 时写映射）。
        /// </summary>
        /// <param name="jpeg">已通过 WebTabletText.IsJpeg 与大小检查的 JPEG 字节。</param>
        /// <returns>null 表示已发出；否则为拒绝原因。</returns>
        public static string SendPhoto(byte[] jpeg)
        {
            if (CurrentState() != EGameState.Trial) return "只有学级裁判阶段可以发送照片";
            if (Managers.Game != null && Managers.Game.IsSpectator) return "观战中不能发送照片";
            var trial = (Managers.UI?.SceneUI as UI_GameScene)?.TrialUI;
            if (trial == null || trial.State != ETrialState.Discuss) return "只能在讨论阶段发送照片";
            int cooldown = WebTabletStore.PhotoCooldownMs();
            if (cooldown > 0) return $"照片冷却中，{(cooldown + 999) / 1000} 秒后可再发";
            var sink = Managers.Network?.GameServer;
            if (sink == null) return "未连接到游戏服务器";
            sink.Send(new C_CHAT_PHOTO
            {
                SlotIndex = -1,
                Data = ByteString.CopyFrom(jpeg),
            });
            ConsumePhotoBudget();
            return null;
        }

        /// <summary>
        /// 照片节流预算被扣减后（PhotoManager.ConsumeBudget 后缀）记录冷却截止时刻。
        /// </summary>
        /// <remarks>
        /// 读取 PhotoManager 的 _budgetTokens（0.1.18c PhotoManager.cs:49）与 _budgetTickMs（:51）：
        /// 两者在 RefillBudget（:100-111）中一起更新，因此可以据此算出回满一格所需的时间。须主线程调用。
        /// </remarks>
        /// <param name="photo">游戏的照片管理器。</param>
        public static void RecordPhotoBudget(PhotoManager photo)
        {
            if (photo == null) return;
            var traverse = Traverse.Create(photo);
            float tokens = traverse.Field("_budgetTokens").GetValue<float>();
            int tickMs = traverse.Field("_budgetTickMs").GetValue<int>();
            float missing = Math.Max(0f, 1f - tokens);
            int waitMs = (int)Math.Ceiling(missing / PhotoRefillPerMs);
            WebTabletStore.SetPhotoReadyTick(unchecked(tickMs + waitMs));
        }

        private static void ConsumePhotoBudget()
        {
            if (ConsumePhotoBudgetMethod == null || Managers.Photo == null) return;
            ConsumePhotoBudgetMethod.Invoke(Managers.Photo, null);
        }

        /// <summary>
        /// 阶段中文名，与平板标记文本一致。
        /// </summary>
        /// <param name="state">游戏阶段。</param>
        /// <returns>中文名；NoneState 返回空串（不产生标记）。</returns>
        public static string StateLabel(EGameState state)
        {
            switch (state)
            {
                case EGameState.Lobby: return "大厅";
                case EGameState.PickCharacter: return "选人";
                case EGameState.Survive: return "生存阶段";
                case EGameState.Detective: return "侦探阶段";
                case EGameState.Trial: return "学级裁判";
                case EGameState.TotalResult: return "结算";
                default: return "";
            }
        }

        private static EGameState CurrentState()
            => Managers.Game != null ? Managers.Game.State : EGameState.NoneState;

        private static bool IsMine(int playerId)
            => Managers.Player != null && playerId == Managers.Player.MyPlayerID;

        private static string NameOf(Player player)
            => player != null ? player.DisplayName : Managers.GetText("WhoSomeone");

        private static string NowStamp()
            => DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

        private static string WhoText(int playerId)
        {
            if (Define.IsMastermindClueId(playerId)) return Managers.GetText("WhoMastermind");
            if (playerId == BlackClueId) return Managers.GetText("WhoBlack");
            Player player = Managers.Player?.GetPlayerCache(playerId);
            return player != null ? player.DisplayName : Managers.GetText("WhoSomeone");
        }

        private static string WhoAvatar(int playerId)
        {
            if (playerId == 0) return "Someone";
            if (Define.IsMastermindClueId(playerId)) return "Mastermind";
            if (playerId == BlackClueId) return "Black";
            return WebTabletAvatar.OfPlayer(Managers.Player?.GetPlayerCache(playerId));
        }

        private static string WhereText(int roomId)
        {
            if (!Managers.Data.RoomDic.TryGetValue(roomId, out RoomData room)) return "";
            return Managers.GetText(Enum.GetName(typeof(ERoomType), room.Type));
        }

        /// <summary>
        /// 与 UI_ClueSubItem.FormatWhen（0.1.18c UI_ClueSubItem.cs:310）同源，去掉富文本尺寸标签。
        /// </summary>
        /// <param name="time">游戏内分钟数。</param>
        /// <returns>形如 "09:30 am" 的文本。</returns>
        public static string WhenText(int time)
        {
            int hour = time / 60;
            int minute = time % 60;
            string meridiem = hour < 12 ? "am" : "pm";
            if (hour > 12) hour -= 12;
            return $"{hour:00}:{minute:00} {meridiem}";
        }

        private static string PropositionSentence(int ownerId, PropositionInfo info)
        {
            try
            {
                var clue = new ClueManager.Clue { IsStatement = true, OwnerId = ownerId, Info = info };
                return WebTabletText.StripRichText(Util.FormatProposition(clue));
            }
            catch (Exception ex)
            {
                Log.Warn("WebTablet", ex, "命题句生成失败，改用空句");
                return "";
            }
        }
    }
}
