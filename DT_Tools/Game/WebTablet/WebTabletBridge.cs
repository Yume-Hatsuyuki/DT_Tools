using System;
using System.Globalization;
using System.Reflection;
using Data;
using Google.Protobuf;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Game.WebTablet
{
    public static class WebTabletBridge
    {
        private const int BlackClueId = 66613;

        private const float PhotoRefillPerMs = 0.2f / 1000f;

        private static readonly MethodInfo ConsumePhotoBudgetMethod = AccessTools.Method(typeof(PhotoManager), "ConsumeBudget");

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

        public static void RecordStateChange(EGameState state)
            => WebTabletStore.SetState(state.ToString(), StateLabel(state), NowStamp());

        public static void SyncState() => RecordStateChange(CurrentState());

        public static string SendChat(string text)
        {
            EGameState state = CurrentState();
            string block = WebTabletText.TypeBlockReason(state.ToString(), StateLabel(state));
            if (block != null) return block;
            if (Managers.Voice == null) return "语音系统尚未就绪";
            Managers.Voice.SendChatMessage(text);
            return null;
        }

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
