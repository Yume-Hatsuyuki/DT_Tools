using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.LobbySpeed
{
    [PatchFeature(
        "大厅移速调整：可修改大厅内移动倍率（默认 1.0）。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Tags = FeatureTag.PlayerMove,
        Author = "梦初雪")]
    public sealed class LobbySpeedFeature
    {
        [Config("大厅内移动速度倍率，游戏默认为 1.0。", Min = 0f)]
        public static float LobbyDeltaSpeed = 1.0f;
    }
}
