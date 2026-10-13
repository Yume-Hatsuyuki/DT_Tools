using System;
using DT_Tools.Core;
using Protocol;

namespace DT_Tools.Features.SitOnChairs
{
    /// <summary>
    /// 岛屿地图集的运行时切换：开启时切到 Island（校园数据 + 11 把椅子），关闭时切回 School。
    /// </summary>
    /// <remarks>
    /// 地图集只在进入房间时加载（UI_LobbyScene 调用 LoadMapSet(School)，会被 LoadMapSetPatch 重定向），
    /// 所以中途开启不会自动生效，椅子数据不存在于设备表中，LocalChairSpawn 会静默跳过。
    /// 这里在大厅内立即重载一次，效果与进入房间相同。局内不换数据，避免设备表在对局中途变化；
    /// 局内开关只记录，下次进入房间时生效。
    /// </remarks>
    internal static class IslandMapSwitch
    {
        /// <summary>
        /// 按开关状态切换地图集。由 SitOnChairsFeature.OnEnabled / OnDisabled 在主线程调用。
        /// </summary>
        /// <param name="enable">true=切到岛屿地图集；false=切回校园地图集。</param>
        internal static void Apply(bool enable)
        {
            try
            {
                if (Managers.Data == null || Managers.Game == null)
                {
                    return;
                }
                if (Managers.Game.State != EGameState.Lobby)
                {
                    Log.Info<SitOnChairsFeature>("当前不在大厅：开关已记录，地图数据将在下次进入房间时生效。");
                    return;
                }
                if (enable)
                {
                    if (Managers.Data.CurrentMapType == EMapType.Island)
                    {
                        return;
                    }
                    if (!IslandData.Inject())
                    {
                        Log.Error<SitOnChairsFeature>("岛屿数据注入失败，未切换地图。");
                        return;
                    }
                    Managers.Data.LoadMapSet(EMapType.Island);
                    Log.Info<SitOnChairsFeature>("大厅内已切换为岛屿地图数据（含椅子），开局后生效。");
                }
                else
                {
                    if (Managers.Data.CurrentMapType != EMapType.Island)
                    {
                        return;
                    }
                    Managers.Data.LoadMapSet(EMapType.School);
                    Log.Info<SitOnChairsFeature>("大厅内已恢复校园地图数据。");
                }
            }
            catch (Exception ex)
            {
                Log.Error<SitOnChairsFeature>(ex, "切换岛屿地图集失败");
            }
        }
    }
}
