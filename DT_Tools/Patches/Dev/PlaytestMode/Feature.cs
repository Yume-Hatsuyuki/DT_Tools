using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.Dev.PlaytestMode
{
    /// <summary>
    /// 测试模式：Define.IsPlaytestApp 恒 true——测试服可更少人数开局
    /// （LOBBY_MIN_PLAYER：0.1.15b Define.cs:2031，playtest 返 0、常态 5；
    /// IsPlaytestApp 判定见 Define.cs:2013-2028）；支付与库存 URL 仍强制走
    /// 正式服常量（0.1.15b Define.cs:380 / :384）。
    /// 影响面：LOBBY_MIN_PLAYER 的消费点在房主侧——开局判定
    /// （0.1.15b Server.Game/GameRoom.cs:1573）与任务规模的人数下限钳制
    /// （0.1.15b Server.Game/MissionManager.cs:362-364），故房主开启即改变
    /// 全房间开局人数下限/任务规模（Define 静态属性按本机 AppId 求值，全房间生效）。
    /// </summary>
    [PatchFeature(
        "测试模式：按测试服逻辑运行（例如可更少人数开局），支付与库存仍走正式服。房主开启会改变全房间开局人数下限/任务规模（全房间生效）。",
        defaultEnabled: false,
        Author = "梦初雪")]
    public sealed class PlaytestModeFeature
    {
    }
}
