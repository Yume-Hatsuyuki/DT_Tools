using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.Experience.EmoteHold
{
    /// <summary>
    /// 表情常驻：发送表情后角色面部表情持续显示，直到发送下一个表情才切换。
    /// 默认同步到全房：保持表情期间周期性重发表情协议（C_USE_EMOTION），
    /// 服务端不断广播后，其他玩家屏幕上你的表情也会持续刷新，全房可见。
    /// 庭审阶段默认不重发（避免庭审刷表情）；开启 SyncInTrial 后庭审也常驻同步。
    /// </summary>
    [PatchFeature(
        "表情常驻：发送表情后面部表情持续显示，直到发送下一个表情；可同步到全房（含可选庭审同步）。",
        defaultEnabled: true)]
    public sealed class EmoteHoldFeature
    {
        [Config("同步到全房：保持表情期间周期性重发表情协议，其他玩家屏幕上表情也会持续显示（走游戏原生协议，全房可见）。")]
        public static bool SyncToOthers = true;

        [Config("庭审同步：庭审阶段也重发表情保持全房可见（默认关，避免庭审刷表情；开启后庭审中表情也常驻，庭审中换表情会跟随）。")]
        public static bool SyncInTrial = false;

        [Config("同步重发间隔（秒）：向全房重发表情的周期，略小于表情 2 秒时长即可保持连续。", Min = 0.8f, Max = 3f)]
        public static float ResendInterval = 1.5f;
    }
}
