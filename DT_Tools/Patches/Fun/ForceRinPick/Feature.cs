using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.Fun.ForceRinPick
{
    /// <summary>
    /// 强制选角任意角色：任意玩家在聊天输入 `!force 玩家名/ID 角色名/ID`，房主进程（服务端）
    /// 把该玩家标记为"下次选角强制为该角色"——Hook 0.1.16b HostPacketHandler.Handle_C_CHAT_MESSAGE
    /// （服务端聊天入口）解析指令并拦截（指令文本不广播全房）；Hook GameRoom.PickCharacter
    /// 在选角时把被标记玩家的 CharacterId 参数改写为目标角色 DataId，且只生效一次
    /// （标记即取即删）。指令省略角色时使用配置 DefaultCharacter。
    /// 限制：若目标角色已被其他玩家选走（服务端角色去重校验），本次强制会静默无效。
    /// </summary>
    [PatchFeature(
        "强制选角任意角色：任意玩家聊天输入 !force 玩家名/ID 角色名/ID，强制该玩家本次选角为该角色（仅生效一次；指令文本不会广播全房）。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        Author = "花语")]
    public sealed class ForceRinPickFeature
    {
        [Config("聊天指令前缀（默认 !force）。", Min = 1, Max = 16)]
        public static string CommandPrefix = "!force";

        [Config("指令省略角色时使用的默认角色名（原版名为 Madeline/Rin/Luna/Jeremy/Hasung/Kaho/Miyuki/Liliana/Seol/Louis/Soi/Noel/Lian，也可用中文名或角色 ID）。")]
        public static string DefaultCharacter = "Rin";
    }
}
