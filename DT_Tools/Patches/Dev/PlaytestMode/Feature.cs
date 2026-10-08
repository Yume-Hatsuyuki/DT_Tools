using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.Dev.PlaytestMode
{
    /// <summary>
    /// 测试模式：Define.IsPlaytestApp 恒 true——测试服可更少人数开局
    /// （LOBBY_MIN_PLAYER：0.1.17a Define.cs:2021，playtest 返 0、常态 5；
    /// IsPlaytestApp 判定见 Define.cs:2003-2017）；库存校验 URL 仍强制走
    /// 正式服常量（0.1.17a Define.cs:384 INVENTORY_CHECK_URL_MAIN）。
    ///
    /// 0.1.17a：Define.PAY_BACKEND_URL 属性已删除（免费货币改 Steam 掉落），
    /// 本功能不再拦截支付 grant URL，仅保留 INVENTORY_CHECK_URL 强制正式服。
    ///
    /// 影响面：LOBBY_MIN_PLAYER 的消费点在房主侧——开局判定与任务规模的人数下限钳制，
    /// 故房主开启即改变全房间开局人数下限/任务规模（Define 静态属性按本机 AppId 求值，全房间生效）。
    /// </summary>
    [PatchFeature(
        "测试模式：按测试服逻辑运行（例如可更少人数开局），库存校验仍走正式服。房主开启会改变全房间开局人数下限/任务规模（全房间生效）。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        Author = "梦初雪")]
    public sealed class PlaytestModeFeature
    {
    }
}
