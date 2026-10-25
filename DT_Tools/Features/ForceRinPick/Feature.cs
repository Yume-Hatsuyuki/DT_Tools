using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.ForceRinPick
{
    [PatchFeature(
        "强制选角任意角色：任意玩家聊天输入 !force 玩家名/ID 角色名/ID，强制该玩家本次选角为该角色（仅生效一次；指令文本不会广播全房）。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        Tags = FeatureTag.PlayerIdentity,
        Author = "花语")]
    public sealed class ForceRinPickFeature
    {
        [Config("聊天指令前缀（默认 !force）。", Min = 1, Max = 16)]
        public static string CommandPrefix = "!force";

        [Config("指令省略角色时使用的默认角色名（原版名为 Madeline/Rin/Luna/Jeremy/Hasung/Kaho/Miyuki/Liliana/Seol/Louis/Soi/Noel/Lian，也可用中文名或角色 ID）。")]
        public static string DefaultCharacter = "Rin";
    }
}
