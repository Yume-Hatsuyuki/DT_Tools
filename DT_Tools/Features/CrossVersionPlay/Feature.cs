using DT_Tools.Core.Attributes;
using HarmonyLib;

namespace DT_Tools.Features.CrossVersionPlay
{
    /// <summary>
    /// 跨版本联机：把大厅匹配用的版本标识改为 CompatBuildId，使不同游戏版本的房主与玩家可以互相搜索、加入。
    /// </summary>
    /// <remarks>
    /// 原版 SteamLobbyManager.GetMyBuildId 写死版本字符串（0.1.20a DummyClient/SteamLobbyManager.cs:157）。
    /// 大厅搜索过滤、建房写入、入房比对、房主端成员校验都读这个返回值，改写它即可覆盖全部比对点（见 Patch.cs）。
    /// 本功能只放宽版本比对，不检查网络包与数据结构是否兼容：版本之间协议不同时会出现不同步、卡死或崩溃。
    /// 房主与玩家都必须开启并填写相同的值，否则比对仍会失败。
    ///
    /// CompatBuildId 默认取「当前游戏版本号」（Define.VERSION）。Define.VERSION 是 const，直接引用会被
    /// 编译期内联成编译目标（0.1.20a）的字面量，故用反射读取运行时值，使默认值随游戏版本自动跟随、不固定。
    /// 留空则不改写（等同功能关闭）。
    /// 大厅 build 在建房与入房时写入，修改配置后需要重建房间，其它玩家也要重新入房。
    /// </remarks>
    [PatchFeature(
        "跨版本联机：统一大厅匹配用的版本标识，使不同游戏版本可以互相加入房间。协议不同时可能不同步或崩溃；房主与玩家都需开启并填写相同的标识。",
        defaultEnabled: false,
        side: FeatureSide.Both,
        Tags = FeatureTag.SysNetwork | FeatureTag.SysRoom,
        Author = "梦初雪")]
    public sealed class CrossVersionPlayFeature
    {
        [Config("大厅匹配用的版本标识。房主与玩家须填写相同的值。默认取当前游戏版本号（Define.VERSION，反射读取，随版本自动跟随）；留空则不改写。")]
        public static string CompatBuildId = DefaultCompatBuildId();

        /// <summary>
        /// 默认大厅版本标识：取当前游戏版本号 Define.VERSION。
        /// Define.VERSION 为 const（编译期内联成编译目标字面量），故用反射读运行时值，随当前游戏版本自动跟随。
        /// 解析失败时返回空串 → 按「留空不改写」处理（等同功能关闭）。
        /// </summary>
        private static string DefaultCompatBuildId()
        {
            try
            {
                var field = AccessTools.Field(typeof(Define), "VERSION");
                string value = field?.GetValue(null) as string;
                if (!string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            }
            catch
            {
                // 忽略：走兜底
            }
            return "";
        }
    }
}
