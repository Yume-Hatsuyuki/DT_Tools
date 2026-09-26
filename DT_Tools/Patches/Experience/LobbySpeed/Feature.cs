using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.Experience.LobbySpeed
{
    /// <summary>
    /// 大厅移速：FixedUpdateMove 的 deltaSpeed 倍率仅在 Lobby 下改写
    /// （原版默认 1，速度 = PrivateInfo.Speed * deltaSpeed，0.1.15b MyPlayer.cs:1869 / :1880）。
    /// </summary>
    [PatchFeature(
        "大厅移速调整：可修改大厅内移动倍率（默认 1.0）。",
        defaultEnabled: false,
        Author = "梦初雪")]
    public sealed class LobbySpeedFeature
    {
        [Config("大厅内移动速度倍率，游戏默认为 1.0。", Min = 0f)]
        public static float LobbyDeltaSpeed = 1.0f;
    }
}
