using DT_Tools.Core.Attributes;
using UnityEngine;

namespace DT_Tools.Patches.Fun.LiveDraw
{
    /// <summary>
    /// 庭审画布"动态照片"（原 DT_LiveDraw v1.0.0 移植）：庭审讨论（作画）阶段按 F9
    /// 把照片目录里的下一张照片自动转成单色线稿画上画布（带"画入场"动画），画完后
    /// 整幅画持续"沸腾"（手绘抖动），每隔几秒自动来一次波浪扭动。
    /// 走原版 DrawingManager 通道（预算/限速合规），其他玩家无需装本 mod 即可看到动画。
    ///
    /// 硬限制（协议层，无法绕过）：
    /// - 单色：画布颜色 = 画图者角色专属色，照片只能是单色线稿；
    /// - 仅庭审讨论阶段可画（房主权威校验 CanRelayDrawing）；
    /// - 死亡状态下画的内容只广播给观战者（活人看不见）；
    /// - 画布 879×696（X±439.5 / Y±348），笔宽 2–16，每人 256 笔 / 8000 点。
    ///
    /// 与照片发送器（PhotoSend）共用同一照片目录（默认 BepInEx/plugins/DT_Tools/Photos/），
    /// 同一批照片既可在庭审发图也可上画布。阶段判断用客户端 TrialManager 单例
    /// （Server.Game.TrialManager.Instance，0.1.16b trial.cs:41 s_instance；State 为独立
    /// 字段不依赖 GameRoom，非房主进程同样安全），不用服务端 GameRoom.Instance——
    /// 原版插件用服务端判断导致非房主画不了，此处修正为任何玩家可用。
    /// </summary>
    [PatchFeature(
        "庭审画布动态照片：庭审讨论阶段按 F9 把 DT_Tools/Photos 里的照片转成单色线稿画上画布（带画入场动画），" +
        "画完持续沸腾抖动+周期波浪，其他玩家无需装本 mod 即可看到动画。F10 沸腾开关 / F11 手动波浪。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Author = "花语")]
    public sealed class LiveDrawFeature
    {
        [Config("画下一张照片（清空自己画布后重画，按文件名顺序轮换）。设为 None 关闭。")]
        public static KeyCode DrawKey = KeyCode.F9;

        [Config("沸腾动画 开/关。设为 None 关闭。")]
        public static KeyCode AnimToggleKey = KeyCode.F10;

        [Config("手动来一次波浪（需已画完在沸腾中）。设为 None 关闭。")]
        public static KeyCode WaveKey = KeyCode.F11;

        [Config("弹跳闪光：整幅画快速放大弹跳一下（需已画完）。设为 None 关闭。")]
        public static KeyCode BounceKey = KeyCode.K;

        [Config("逐笔重播：线条像写字一样重新一笔笔出现（需已画完）。设为 None 关闭。")]
        public static KeyCode ReplayKey = KeyCode.L;

        [Config("心跳呼吸：整幅画按心跳节奏缩放脉动（需已画完）。设为 None 关闭。")]
        public static KeyCode HeartbeatKey = KeyCode.J;

        [Config("漂浮纸片：整幅画像纸片一样缓慢上下漂移摇晃，再按一次停止。设为 None 关闭。")]
        public static KeyCode FloatKey = KeyCode.H;

        [Config("溶解消散：线条按随机顺序逐笔消失再重聚（需已画完）。设为 None 关闭。")]
        public static KeyCode DissolveKey = KeyCode.U;

        [Config("沸腾帧率（每秒 REPLACE 包数，1-12；人多/房主网络差就调低）。", Min = 1, Max = 12)]
        public static int BoilFps = 6;

        [Config("每 N 秒自动来一次波浪，0=关闭。", Min = 0, Max = 600)]
        public static int AutoWaveEverySeconds = 6;

        [Config("单张照片最多笔画数（协议上限 256）。", Min = 1, Max = 256)]
        public static int MaxStrokes = 230;

        [Config("单张照片最多点数（协议上限 8000，越多画得越久）。", Min = 1, Max = 8000)]
        public static int MaxPoints = 3400;

        [Config("线稿笔宽（主轮廓，2-16；次轮廓自动取 60%）。越小线条越细。", Min = 2, Max = 16)]
        public static float StrokeWidth = 3.5f;

        [Config("照片目录（绝对路径，例如 D:/我的照片，jpg/png 按文件名轮换）。留空用默认目录 BepInEx/plugins/DT_Tools/Photos/。")]
        public static string PhotoFolder = "";
    }
}
