using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.Shop.UnlockMadeline
{
    /// <summary>
    /// 解锁 Madeline(101) 并补齐缺失资源/UI：
    ///   - 所有权：SteamInventorySource / SaveManager 对 101 恒 true，OwnedCharacterIds 追加 101
    ///   - 商店/选人 UI：列表重建、排序、介绍文案、技能信息立绘
    ///   - 档案/审判：立绘位置表、过场表情图
    ///   - 资源：ResourceManager 就绪后注入裁切出的缺失贴图（Ui.cs）
    /// 与 UnlockCharacters / UnlockEmotes 互补，可叠加。
    /// </summary>
    [PatchFeature(
        "梅德琳解锁：解锁梅德琳并修复缺失资源。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Author = "梦初雪")]
    public sealed class UnlockMadelineFeature
    {
        private static void OnEnabled()
        {
            // 热开启补注入：IsInit 是自动属性且只在启动时置 true 一次
            //（0.1.15b ResourceManager.cs:17），错过 setter 时在此手动触发
            if (Managers.Resource != null && Managers.Resource.IsInit)
                UnlockMadelineUi.InjectAll(Managers.Resource);
        }

        private static void OnDisabled()
        {
            // 副作用清理：移除注入键 / 销毁裁切贴图 / 还原位置表
            UnlockMadelineUi.Cleanup();
        }
    }
}
