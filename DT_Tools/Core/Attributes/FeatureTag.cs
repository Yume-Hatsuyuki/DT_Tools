using System;

namespace DT_Tools.Core.Attributes
{
    /// <summary>
    /// 功能标签（多选，每个单元 1–3 个）。
    /// </summary>
    /// <remarks>
    /// bit 位一经发布不得重排或复用，新增只能追加到末尾。
    /// 分组、中文名与说明见 FeatureTagCatalog。
    /// </remarks>
    [Flags]
    public enum FeatureTag : ulong
    {
        None = 0,
        PlayerMove = 1UL << 0,
        PlayerCombat = 1UL << 1,
        PlayerAbility = 1UL << 2,
        PlayerIdentity = 1UL << 3,
        PlayerEmote = 1UL << 4,
        PlayerVisual = 1UL << 5,
        UiTablet = 1UL << 6,
        UiMapPin = 1UL << 7,
        UiChat = 1UL << 8,
        UiHud = 1UL << 9,
        UiPopup = 1UL << 10,
        UiInput = 1UL << 11,
        UiDraw = 1UL << 12,
        UiShop = 1UL << 13,
        GameRound = 1UL << 14,
        GameVote = 1UL << 15,
        GameMission = 1UL << 16,
        GameDevice = 1UL << 17,
        GameFuse = 1UL << 18,
        GameDarkness = 1UL << 19,
        GameDoor = 1UL << 20,
        GameWeapon = 1UL << 21,
        GameSabotage = 1UL << 22,
        GameEvidence = 1UL << 23,
        GameKill = 1UL << 24,
        GameCorpse = 1UL << 25,
        GameGhost = 1UL << 26,
        GameIntel = 1UL << 27,
        GameBuff = 1UL << 28,
        SysRoom = 1UL << 29,
        SysHost = 1UL << 30,
        SysNetwork = 1UL << 31,
        SysAudio = 1UL << 32,
        SysEconomy = 1UL << 33,
        SysBackend = 1UL << 34,
        SysData = 1UL << 35,
        SysAutomation = 1UL << 36,
        SysDebug = 1UL << 37,
        Peripheral = 1UL << 38,
    }
}
