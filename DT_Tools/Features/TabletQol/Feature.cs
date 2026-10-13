using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.TabletQol
{
    /// <summary>
    /// 会议/调查阶段平板（`UI_GameTablet`）的两项体验优化，合成一个配置段。
    /// </summary>
    /// <remarks>
    /// 纯客户端（FeatureSide.Client）：只改自己屏幕上的 UI，不影响别人，也不需要房主装。
    /// ① 滚动条：聊天/线索列表加可拖拽滚动条。游戏从不用 UnityEngine.UI.Scrollbar（0.1.17a 反编译转储全文 0 处），列表只靠 ScrollRect 的拖拽/滚轮（UI_ScrollDrag，转储 :68540 附近），长列表翻页很累。默认贴边（BarOffset=0），可点轨道翻页（BarTrackClickPages）。
    /// ② 「提交线索」二次确认：PropositionButton 一点就提交且不可撤回（UI_ClueSubItem.OnClickPropositionButton 直接发 C_SUBMIT_PROPOSITION，转储 :86488-86500）。拦下来先弹确认栏，栏内内嵌一张原版线索卡片（照抄 MakeSubItem&lt;UI_ClueSubItem> + SetInfo + InitPopupClue，转储 :52838-52840），按钮也克隆原版，视觉与原版一致。
    /// 两个列表（_chatScroll / ClueSrollRect）在会议与调查里是同一个 UI_GameTablet 的同一块，两阶段默认都开「线索」页（DefaultSection() / IsInvestigation，转储 :52064-52071、:51479-51486），因此挂两个点（Init 与 Section setter）才能两阶段都生效。
    /// </remarks>
    [PatchFeature(
        "会议/调查阶段平板两项优化：① 聊天/线索列表加可拖拽滚动条（贴边、可点轨道翻页）；② 点「提交线索」先弹二次确认栏（内嵌一张原版线索卡片，确认后才提交）。两项可分别开关（客户端，自己装生效）。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Tags = FeatureTag.UiTablet | FeatureTag.UiPopup | FeatureTag.UiChat,
        Author = "crimsonote")]
    public sealed class TabletQolFeature
    {
        [Config("① 滚动条：给平板列表加可拖拽滚动条。")]
        public static bool Scrollbar = true;

        [Config("① 滚动条：给「聊天」列表挂。")]
        public static bool ScrollbarChat = true;

        [Config("① 滚动条：给「线索」列表挂。")]
        public static bool ScrollbarClue = true;

        [Config("① 滚动条：宽度（像素）。", Min = 4, Max = 40)]
        public static int BarWidth = 10;

        [Config("① 滚动条：与列表边缘的距离（像素，0 = 贴边）。", Min = 0, Max = 60)]
        public static int BarOffset = 0;

        [Config("① 滚动条：常驻显示。关闭 = 只在列表内容超出可视范围时出现。")]
        public static bool BarAlwaysVisible = true;

        [Config("① 滚动条：点轨道空白处翻一页（更省事）。关闭 = 只能拖手柄，且轨道不抢点击。")]
        public static bool BarTrackClickPages = true;

        [Config("② 二次确认：点「提交线索」先弹确认栏（提交不可撤回）。")]
        public static bool ConfirmSubmit = true;

        [Config("② 二次确认：确认栏里内嵌一张原版线索卡片（显示该线索的谁/在哪/何时与正文）。")]
        public static bool ConfirmShowCard = true;

        [Config("② 二次确认：遮罩不透明度（0–1）。", Min = 0f, Max = 1f)]
        public static float DimAlpha = 0.45f;

        [Config("② 二次确认：确认栏面板底色（HTML 颜色）。默认 #22303F = 从游戏内平板线索卡片上取到的同一支蓝。")]
        public static string PanelColorHex = "#22303F";

        [Config("② 二次确认：确认栏面板的不透明度（0–1，1 = 完全不透明）。", Min = 0f, Max = 1f)]
        public static float PanelAlpha = 0.86f;

        [Config("② 二次确认：两个按钮的不透明度（0–1，1 = 纯白实心）。", Min = 0.2f, Max = 1f)]
        public static float ButtonAlpha = 0.85f;
    }
}
