using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.PlaytestMode
{
    /// <summary>
    /// 测试模式：Define.IsPlaytestApp 恒 true，测试服可更少人数开局（LOBBY_MIN_PLAYER：0.1.18c Define.cs:2023，playtest 返 0、常态 5；IsPlaytestApp 判定见 Define.cs:2005-2013）；库存校验 URL 仍强制走正式服常量（0.1.18c Define.cs:384 INVENTORY_CHECK_URL_MAIN）。
    /// </summary>
    /// <remarks>
    /// 0.1.18c：Define 中从未存在 PAY_BACKEND_URL 属性（两版本仅有 PAY_BACKEND_URL_MAIN / _PLAYTEST 常量，且未被引用，Define.cs:380-382）；免费货币走 Steam 掉落（FreeCurrencyDrops.cs:240）。本功能只覆盖 INVENTORY_CHECK_URL 的 getter（Define.cs:1993）。
    /// 影响面：LOBBY_MIN_PLAYER 的消费点是 UI_GameScene.cs:2064（客户端开局按钮）、GameRoom.cs:1573（HandleStart）与 MissionManager.cs:362/364（任务规模钳制）。本功能覆盖 IsPlaytestApp（Define.cs:2005，判定为本机 Steam AppID == 4395210），原版 LOBBY_MIN_PLAYER 随之返回 0（Define.cs:2027-2029）。是否全房间生效取决于运行这些消费点的进程，需联机实测。
    /// </remarks>
    [PatchFeature(
        "测试模式：按测试服逻辑运行（例如可更少人数开局），库存校验仍走正式服。房主开启会改变开局人数下限/任务规模（需联机确认是否全房间生效）。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        Tags = FeatureTag.SysDebug | FeatureTag.SysRoom,
        Author = "梦初雪")]
    public sealed class PlaytestModeFeature
    {
    }
}
