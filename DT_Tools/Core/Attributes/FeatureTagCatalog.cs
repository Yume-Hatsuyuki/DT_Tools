using System;
using System.Collections.Generic;
using System.Linq;

namespace DT_Tools.Core.Attributes
{
    /// <summary>
    /// 功能标签字典：分组、中文名、说明。
    /// </summary>
    /// <remarks>
    /// 枚举值只在 FeatureTag 定义，这里只做展示与校验。
    /// 新增标签时两处同步：FeatureTag 追加一位，这里追加一条。
    /// </remarks>
    public static class FeatureTagCatalog
    {
        /// <summary>
        /// 单个单元允许的最多标签数。
        /// </summary>
        public const int MaxPerUnit = 3;

        public sealed class Entry
        {
            public FeatureTag Tag;
            public string Key;
            public string Group;
            public string Label;
            public string Description;
        }

        public static readonly IReadOnlyList<Entry> Entries = new[]
        {
                new Entry { Tag = FeatureTag.PlayerMove, Key = "PlayerMove", Group = "玩家", Label = "移动与位置", Description = "移速、穿墙、瞬移、出生点" },
                new Entry { Tag = FeatureTag.PlayerCombat, Key = "PlayerCombat", Group = "玩家", Label = "攻击与近战", Description = "攻击距离、攻击判定、近战击杀能力" },
                new Entry { Tag = FeatureTag.PlayerAbility, Key = "PlayerAbility", Group = "玩家", Label = "角色能力", Description = "角色专属技能与被动（Hasung、KAHO、Seol、SOI、Louis、本杰明）" },
                new Entry { Tag = FeatureTag.PlayerIdentity, Key = "PlayerIdentity", Group = "玩家", Label = "身份", Description = "阵营、名字、昵称、选角、角色展示" },
                new Entry { Tag = FeatureTag.PlayerEmote, Key = "PlayerEmote", Group = "玩家", Label = "表情与动作", Description = "表情、表情栏、动作、坐姿" },
                new Entry { Tag = FeatureTag.PlayerVisual, Key = "PlayerVisual", Group = "玩家", Label = "角色视觉", Description = "残影、果冻、角色特效" },
                new Entry { Tag = FeatureTag.UiTablet, Key = "UiTablet", Group = "界面", Label = "平板", Description = "平板各页面与操作" },
                new Entry { Tag = FeatureTag.UiMapPin, Key = "UiMapPin", Group = "界面", Label = "地图标记", Description = "小地图与平板上的图标、Pin" },
                new Entry { Tag = FeatureTag.UiChat, Key = "UiChat", Group = "界面", Label = "聊天与消息", Description = "聊天框、浮文、短信、打字机显示" },
                new Entry { Tag = FeatureTag.UiHud, Key = "UiHud", Group = "界面", Label = "常驻显示", Description = "HUD、小窗、角落状态、延迟显示" },
                new Entry { Tag = FeatureTag.UiPopup, Key = "UiPopup", Group = "界面", Label = "弹窗", Description = "确认框、通报、设置窗" },
                new Entry { Tag = FeatureTag.UiInput, Key = "UiInput", Group = "界面", Label = "输入框", Description = "字数、格式限制" },
                new Entry { Tag = FeatureTag.UiDraw, Key = "UiDraw", Group = "界面", Label = "画布", Description = "画线、画布照片" },
                new Entry { Tag = FeatureTag.UiShop, Key = "UiShop", Group = "界面", Label = "商店界面", Description = "商店页签、购买与配置页" },
                new Entry { Tag = FeatureTag.GameRound, Key = "GameRound", Group = "玩法", Label = "对局流程", Description = "阶段切换、开局与结束条件、时间" },
                new Entry { Tag = FeatureTag.GameVote, Key = "GameVote", Group = "玩法", Label = "投票与审判", Description = "裁判投票、庭审阶段" },
                new Entry { Tag = FeatureTag.GameMission, Key = "GameMission", Group = "玩法", Label = "任务", Description = "任务派发与进度" },
                new Entry { Tag = FeatureTag.GameDevice, Key = "GameDevice", Group = "玩法", Label = "设备", Description = "椅子、货架、钓鱼、矿机、电话、打字机等设备交互" },
                new Entry { Tag = FeatureTag.GameFuse, Key = "GameFuse", Group = "玩法", Label = "电闸与断电", Description = "电箱、停电、黑暗来源" },
                new Entry { Tag = FeatureTag.GameDarkness, Key = "GameDarkness", Group = "玩法", Label = "黑暗与视野", Description = "照明、视距、迷雾" },
                new Entry { Tag = FeatureTag.GameDoor, Key = "GameDoor", Group = "玩法", Label = "门", Description = "开关、锁门" },
                new Entry { Tag = FeatureTag.GameWeapon, Key = "GameWeapon", Group = "玩法", Label = "武器与道具", Description = "刀、武器架、递刀、手持道具" },
                new Entry { Tag = FeatureTag.GameSabotage, Key = "GameSabotage", Group = "玩法", Label = "破坏行为", Description = "破坏按钮、破坏权限、留痕" },
                new Entry { Tag = FeatureTag.GameEvidence, Key = "GameEvidence", Group = "玩法", Label = "证据与线索", Description = "线索扫描、销毁证据、拍照留证" },
                new Entry { Tag = FeatureTag.GameKill, Key = "GameKill", Group = "玩法", Label = "击杀与死亡", Description = "击杀、毒杀、战报、死亡判定" },
                new Entry { Tag = FeatureTag.GameCorpse, Key = "GameCorpse", Group = "玩法", Label = "尸体与调查", Description = "尸体发现、等待、报警" },
                new Entry { Tag = FeatureTag.GameGhost, Key = "GameGhost", Group = "玩法", Label = "幽灵", Description = "死后交互、幽灵视角、亡者通讯" },
                new Entry { Tag = FeatureTag.GameIntel, Key = "GameIntel", Group = "玩法", Label = "情报与信息差", Description = "谁能看到什么、阵营与存活列表" },
                new Entry { Tag = FeatureTag.GameBuff, Key = "GameBuff", Group = "玩法", Label = "增益与状态", Description = "BUFF、护盾、技能偷取" },
                new Entry { Tag = FeatureTag.SysRoom, Key = "SysRoom", Group = "系统", Label = "房间与大厅", Description = "建房、加入、人数、房间码、观战" },
                new Entry { Tag = FeatureTag.SysHost, Key = "SysHost", Group = "系统", Label = "房主管理", Description = "房主权限、踢人、封禁、转让、房主侧过滤" },
                new Entry { Tag = FeatureTag.SysNetwork, Key = "SysNetwork", Group = "系统", Label = "网络与 P2P", Description = "中继、连接、玩家网络信息、大厅列表" },
                new Entry { Tag = FeatureTag.SysAudio, Key = "SysAudio", Group = "系统", Label = "音频", Description = "语音、阶段音乐、点播、虚拟麦克风、MP3 播放器" },
                new Entry { Tag = FeatureTag.SysEconomy, Key = "SysEconomy", Group = "系统", Label = "经济", Description = "商店库存、解锁、奖励、货币" },
                new Entry { Tag = FeatureTag.SysBackend, Key = "SysBackend", Group = "系统", Label = "后端", Description = "反代、Discord、上报" },
                new Entry { Tag = FeatureTag.SysData, Key = "SysData", Group = "系统", Label = "数据", Description = "存档、配置、导入导出" },
                new Entry { Tag = FeatureTag.SysAutomation, Key = "SysAutomation", Group = "系统", Label = "自动化", Description = "无需人工操作的自动行为" },
                new Entry { Tag = FeatureTag.SysDebug, Key = "SysDebug", Group = "系统", Label = "开发调试", Description = "测试模式、协议自检、截图、日志、帮助" },
                new Entry { Tag = FeatureTag.Peripheral, Key = "Peripheral", Group = "外设", Label = "外设与输入", Description = "鼠标、键盘、手柄、宏、灵敏度、按键映射" },
        };

        /// <summary>
        /// 所有已定义标签的并集。
        /// </summary>
        public static readonly ulong AllBits = Entries.Aggregate(0UL, (acc, e) => acc | (ulong)e.Tag);

        /// <summary>
        /// 校验单元的标签：至少 1 个、最多 MaxPerUnit 个、只含已定义的标签。
        /// </summary>
        /// <remarks>
        /// 不合格直接抛出——与命名空间重复一样属于声明错误，禁止兜底。
        /// </remarks>
        /// <param name="tags">单元声明的标签集合（位标志）。</param>
        /// <param name="owner">声明这些标签的单元名，用于校验失败时定位。</param>
        public static void Validate(FeatureTag tags, string owner)
        {
            ulong bits = (ulong)tags;
            if (bits == 0)
                throw new InvalidOperationException($"{owner} 缺少 Tags：每个单元至少声明 1 个标签。");
            if ((bits & ~AllBits) != 0)
                throw new InvalidOperationException($"{owner} 的 Tags 含未定义的位：0x{bits & ~AllBits:X}。");
            int count = 0;
            for (ulong b = bits; b != 0; b &= b - 1) count++;
            if (count > MaxPerUnit)
                throw new InvalidOperationException($"{owner} 的 Tags 有 {count} 个，上限 {MaxPerUnit} 个。请只保留最主要的领域。");
        }

        /// <summary>
        /// 标签 → 键名数组（按字典顺序），供 JSON 与 WebUI 使用。
        /// </summary>
        /// <param name="tags">要转换的标签集合（位标志）。</param>
        /// <returns>标签的键名数组，按字典顺序排列。</returns>
        public static string[] Names(FeatureTag tags)
        {
            if (tags == FeatureTag.None) return new string[0];
            return Entries.Where(e => (tags & e.Tag) != 0).Select(e => e.Key).ToArray();
        }
    }
}
