using System;
using DT_Tools.Core;
using Protocol;

namespace DT_Tools.Features.SitOnChairs
{
    internal static class IslandMapSwitch
    {
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
