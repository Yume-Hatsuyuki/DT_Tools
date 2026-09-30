using DT_Tools.Core;
using DT_Tools.Core.Attributes;

namespace DT_Tools.Automation.AutoPickCharacter
{
    /// <summary>大厅同步外观 + 选角阶段自动 C_PICK_CHARACTER（角色列表运行时读 CharacterDic，含梅德琳）。</summary>
    [AutomationModule("自动选择角色", "大厅使用所选角色模型；进入选角阶段后自动发送 C_PICK_CHARACTER（指定或随机）。", Author = "梦初雪")]
    public sealed class AutoPickCharacterModule
    {
        [Config("选角方式：Fixed=使用下方角色；Random=随机（发包 CharacterId=-2）。")]
        public static AutoPickMode Mode = AutoPickMode.Fixed;

        [Config("指定角色 DataId（含 101 梅德琳）；WebUI 以下拉展示角色名。")]
        public static int CharacterId = 102;

        [Config("进入选角阶段后延迟多少秒再开始发包（需等服务端选角就绪）。", Min = 0, Max = 60)]
        public static float DelaySeconds = 1.2f;

        [Config("发包后仍未确认时的重试间隔（秒）。过早包会被服务端忽略。", Min = 0.1f, Max = 60)]
        public static float RetryInterval = 0.8f;

        [Config("本阶段最多发送次数。达到后停止（避免无限刷包）；大厅换角重试共用该上限。", Min = 1, Max = 9999)]
        public static int MaxAttempts = 20;

        [Config("Fixed 模式下，后半次尝试改为随机（-2），避免目标角色已被他人占用。")]
        public static bool FallbackToRandom = true;

        [Config("在大厅时发送 ChangeCharacter，使自己的模型与所选角色一致。")]
        public static bool SyncLobby = true;

        public static void Tick(bool hostEnabled)
        {
            if (!hostEnabled || !Engine.Enabled<AutoPickCharacterModule>())
            {
                AutoPickCharacterState.Reset();
                return;
            }
            AutoPickCharacterTrigger.Tick();
        }

        private static void OnLoaded()
            => OptionProviders.Bind(
                Engine.SectionOf<AutoPickCharacterModule>(),
                "CharacterId",
                AutoPickCharacterLogic.Options);
    }
}
