using DT_Tools.Core.Attributes;
using DT_Tools.Game.WebTablet;

namespace DT_Tools.Features.WebTablet
{
    /// <summary>
    /// 平板电脑（实验性）：采集聊天、命题、线索、照片与阶段切换，供 WebUI「平板电脑」应用展示、导出与发言。
    /// </summary>
    /// <remarks>
    /// 补丁只做采集，不改变游戏逻辑。关闭时清空存储，阶段记录一并忘记，重新启用后由 SyncState 补齐。
    /// 发言与照片经 WebConsole 发出，协议与游戏内平板相同（见 Game/WebTablet/WebTabletBridge.cs）。
    /// </remarks>
    [PatchFeature(
        "平板电脑（实验性）：WebUI 新增「平板电脑」应用，左侧聊天、右侧线索；记录阶段切换，支持导出文本或图片、清理，并可在大厅与学级裁判发言。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Tags = FeatureTag.UiTablet | FeatureTag.UiChat,
        Author = "梦初雪")]
    public sealed class WebTabletFeature
    {
        /// <summary>
        /// 热关闭：清空已采集的内容与阶段记录（AGENTS.md §8 副作用清理）。
        /// </summary>
        private static void OnDisabled() => WebTabletStore.Reset();
    }
}
