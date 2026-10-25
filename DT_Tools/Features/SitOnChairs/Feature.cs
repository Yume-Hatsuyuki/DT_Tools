using System;
using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.SitOnChairs
{
    [PatchFeature(
        "可坐椅子：花园长椅×2、炼金室沙发×1 可坐下/起身（房主装了即全房同步）；等待室咖啡椅默认仅装饰，可开启本地坐；可选开启坐姿同步（SyncSitting）——装本 mod 的玩家互见坐姿，无需房主。按 E 交互，复用游戏自带坐姿动画。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Tags = FeatureTag.GameDevice | FeatureTag.PlayerEmote,
        Author = "花语")]
    public sealed class SitOnChairsFeature
    {
        [Config("等待室咖啡椅是否可坐（false=只作装饰：不显示坐下提示、按 E 无效；true=本地可坐，仅自己看得到坐姿，服务端不建设备）。")]
        public static bool CafeSit = false;

        [Config("坐下时把椅子自己的美术抬到前排墙/窗框之上（255=高于地图 250、低于坐着的人 260），起身立刻还原。")]
        public static bool ChairOnTop = true;

        [Config("坐姿横向微调（世界单位）：正数=人往右。对所有椅子生效。")]
        public static float SeatXOffset = 0f;

        [Config("长椅坐姿纵向微调（世界单位）：负数=人往下坐进椅面。")]
        public static float SeatYOffset = -75f;

        [Config("沙发坐姿纵向微调（世界单位）：正数=人往上（沙发座垫比长椅高）。")]
        public static float SofaYOffset = 53f;

        [Config("等待室咖啡椅坐姿纵向微调（世界单位，仅 CafeSit=true 时有用）。")]
        public static float CafeYOffset = 0f;

        [Config("对局坐姿同步：装本 mod 的玩家之间互见坐下/起身（经聊天广播通道，无需房主、无需其他玩家装 mod）。房主装了仍走原生网络同步。")]
        public static bool SyncSitting = false;

        [Config("调试日志：周期性输出玩家坐标/状态、每把椅子的坐标/距离/是否在交互框内/渲染层级。建议调好后关闭。")]
        public static bool Debug = false;

        internal static void OnEnabled() => IslandMapSwitch.Apply(true);

        internal static void OnDisabled()
        {
            StandUpIfSitting();
            IslandMapSwitch.Apply(false);
        }

        private static void StandUpIfSitting()
        {
            if (!LocalSit.IsSitting && !GameSit.IsSitting)
            {
                return;
            }
            try
            {
                MyPlayer my = Managers.Player?.MyPlayer;
                LocalSit.Stand(my);
                GameSit.Stand(my);
                Log.Info<SitOnChairsFeature>("功能已关闭，已起身。");
            }
            catch (Exception ex)
            {
                Log.Error<SitOnChairsFeature>(ex, "关闭时起身失败");
            }
        }

    }
}
