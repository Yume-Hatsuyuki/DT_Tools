using DT_Tools.Core.Attributes;
using HarmonyLib;

namespace DT_Tools.Features.CrossVersionPlay
{
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
            }
            return "";
        }
    }
}
