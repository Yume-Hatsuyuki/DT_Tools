using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.Experience.CharacterMapPin
{
    /// <summary>
    /// 平板/小地图玩家 Pin 用角色专属 *_Map_Black/White 头像替换通用白点/黑点，
    /// 并可选在世界中常驻指向其他存活玩家的角色箭头（不开平板也可见）。
    ///
    /// 文件分工：State=箭头运行态与轨道参数；Logic=pin 查找/白方强制刷新（pin 同步）；
    /// Ui=箭头对象构建、pin/箭头贴图解析与替换；各 Patch.&lt;主题&gt;.cs 只做门闩与转发。
    /// </summary>
    [PatchFeature(
        "角色头像 Pin：平板与小地图上其他玩家的白点/黑点显示为对应角色头像；可另开常驻角色箭头。" +
        "白方默认不显示他人 Pin，可用「白方也显示他人 Pin」单独打开。",
        defaultEnabled: false,
        Author = "梦初雪")]
    public sealed class CharacterMapPinFeature
    {
        [Config("已识破的黑方玩家黑点也替换为黑色角色头像；关闭则保留原版黑点")]
        public static bool ReplaceBlackPin = true;

        [Config("白方也显示他人 Pin：原版白方小地图只显示自己，开启后强制刷新其他存活玩家的 Pin 并套角色头像")]
        public static bool ShowPinsForWhite = false;

        [Config("常驻角色箭头：不开平板也在屏幕边缘显示指向其他存活玩家的箭头（接近时自动隐藏，Kaho 技能目标不重复显示）")]
        public static bool ShowCharacterArrow = false;

        [Config("死亡保留 Pin：其他玩家死亡后，小地图与平板上的 Pin 不删除，定格在最后位置，" +
                "并像 Kaho 监视目标一样跟随服务器广播的强制位移（躲柜、MindControl 传送、Trick 搬运等）；" +
                "0.1.16b TabletManager.DeletePin 的技能豁免只对 Kaho 生效，本开关把它扩大到所有人；" +
                "庭审开始与结算仍按原版清理")]
        public static bool KeepDeadPin = false;

        /// <summary>
        /// 运行时关闭：销毁全部常驻箭头，并把已替换的 pin 贴图还原为原版。
        /// 原版 RefreshPlayerPin 对已存在的 pin 只调 SetLocalPosition、不重设 sprite
        ///（0.1.16b UI_GameScene.cs:852-868 / UI_GameTablet.cs:1211-1226 的 else 分支），
        /// 所以热关闭后贴图不会自行还原，必须在这里按 PinSpriteKey 主动恢复（见 Ui.RestoreAllPins）。
        /// 死亡保留的 pin 先整批删除（玩家已 Despawn，原版不会替我们清理），再走常规还原。
        /// </summary>
        private static void OnDisabled()
        {
            CharacterMapPinUi.ClearKeptPins();
            CharacterMapPinUi.RestoreAllPins();
            CharacterMapPinUi.ClearAllArrows();
        }
    }
}
