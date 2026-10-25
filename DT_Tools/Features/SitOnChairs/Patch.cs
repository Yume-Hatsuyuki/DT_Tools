using System;
using System.Collections.Generic;
using System.Text;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;
using Spine.Unity;
using UnityEngine;

namespace DT_Tools.Features.SitOnChairs
{
    internal static class Patches
    {

        [HarmonyPatch(typeof(DataManager), "LoadMapSet")]
        internal static class LoadMapSetPatch
        {
            private static void Prefix(ref EMapType type)
            {
                try
                {
                    if (!Engine.Enabled<SitOnChairsFeature>())
                    {
                        return;
                    }
                    if (type != EMapType.School)
                    {
                        return;
                    }
                    if (IslandData.Inject())
                    {
                        type = EMapType.Island;
                        Log.Info<SitOnChairsFeature>("已将 LoadMapSet(School) 重定向为 Island（原版数据+椅子条目）。");
                    }
                    else
                    {
                        Log.Error<SitOnChairsFeature>("岛屿数据注入失败，本次沿用原版学校地图。");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error<SitOnChairsFeature>(ex, "地图重定向异常，沿用原版");
                }
            }
        }

        [HarmonyPatch(typeof(DeviceManager), "Spawn", new[] { typeof(string), typeof(DeviceInfo) })]
        internal static class ChairSpawnPatch
        {
            private static void Prefix(string prefabName)
            {
                if (Engine.Enabled<SitOnChairsFeature>() && prefabName == ChairFactory.PrefabKey)
                {
                    ChairFactory.Ensure();
                }
            }
        }

        [HarmonyPatch(typeof(Chair), "SetInfo")]
        internal static class ChairSetInfoPatch
        {
            private static void Prefix(Chair __instance)
            {
                if (Engine.Enabled<SitOnChairsFeature>())
                {
                    ChairFactory.EnsureSkeleton(__instance != null ? __instance.GetComponentInChildren<SkeletonAnimation>() : null);
                }
            }

            private static void Postfix(Chair __instance)
            {
                if (Engine.Enabled<SitOnChairsFeature>() && __instance != null)
                {
                    ChairFactory.ApplyCharacterSorting(__instance);
                    ChairFactory.ApplySeatOffset(__instance);
                }
            }
        }

        [HarmonyPatch(typeof(Managers), "GetText")]
        internal static class ChairTextPatch
        {
            private static bool Prefix(string textId, ref string __result)
            {
                if (textId == ChairFactory.TextKey)
                {
                    __result = ChairFactory.TextValue;
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(Chair), "GetInteractMessage")]
        internal static class ChairCafeNoSitPatch
        {
            private static void Postfix(Chair __instance, ref string __result)
            {
                if (Engine.Enabled<SitOnChairsFeature>() && !SitOnChairsFeature.CafeSit && ChairFactory.IsCafe(__instance))
                {
                    __result = "";
                }
            }
        }

        [HarmonyPatch(typeof(Chair), "GetInteractMessage")]
        internal static class ChairLocalPromptPatch
        {
            private static void Postfix(Chair __instance, ref string __result)
            {
                if (LocalSit.IsOn(__instance) && !string.IsNullOrEmpty(ChairFactory.TextValue))
                {
                    string[] parts = ChairFactory.TextValue.Split('/');
                    if (parts.Length > 1)
                    {
                        __result = parts[1];
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Chair), "Interact")]
        internal static class ChairLocalSitPatch
        {
            private static bool Prefix(Chair __instance)
            {
                if (!Engine.Enabled<SitOnChairsFeature>())
                {
                    return true;
                }
                if (Managers.Game == null)
                {
                    return true;
                }
                if (Managers.Game.State == EGameState.Lobby)
                {
                    if (LocalSit.Enabled)
                    {
                        LocalSit.Toggle(__instance);
                        return false;
                    }
                    return true;
                }
                if (Managers.Game.State == EGameState.Survive || Managers.Game.State == EGameState.Detective)
                {
                    GameSit.Toggle(__instance);
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(Player), "UpdateAnimation")]
        internal static class LocalSitAnimPatch
        {
            private static bool Prefix()
            {
                return !LocalSit.IsSitting && !GameSit.IsSitting;
            }
        }

        [HarmonyPatch(typeof(MyPlayer), "FixedUpdateMove")]
        internal static class LocalSitMovePatch
        {
            private static bool Prefix()
            {
                if (LocalSit.IsSitting)
                {
                    LocalSit.Hold();
                    return false;
                }
                if (GameSit.IsSitting)
                {
                    GameSit.Hold();
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(DeviceManager), "SearchInteractDevice")]
        internal static class ChairTunePatch
        {
            internal const float Step = 5f;

            private static float _delta;

            private static int _lastFrame = -1;

            private static void Postfix(DeviceManager __instance)
            {
                if (!Engine.Enabled<SitOnChairsFeature>() || __instance == null || __instance.Cache == null ||
                    Time.frameCount == _lastFrame)
                {
                    return;
                }
                _lastFrame = Time.frameCount;

                MyPlayer my = Managers.Player != null ? Managers.Player.MyPlayer : null;
                if (my == null || my.State != EPlayerState.Sit)
                {
                    _delta = 0f;
                    return;
                }

                Chair chair = ChairFactory.NearestChair(__instance, my.transform.position);
                if (chair == null)
                {
                    return;
                }

                bool down = Input.GetKeyDown(KeyCode.LeftBracket);
                bool up = Input.GetKeyDown(KeyCode.RightBracket);
                if (down)
                {
                    _delta -= Step;
                }
                if (up)
                {
                    _delta += Step;
                }

                DeviceInfo info = chair.Info;
                if (info != null && info.Pos != null)
                {
                    chair.transform.position = new Vector3(
                        info.Pos.X + SitOnChairsFeature.SeatXOffset,
                        info.Pos.Y + ChairFactory.OffsetYFor(chair) + _delta,
                        0f);
                    if (down || up)
                    {
                        string key = ChairFactory.IsSofa(chair) ? "SofaYOffset"
                            : ChairFactory.IsCafe(chair) ? "CafeYOffset"
                            : "SeatYOffset";
                        Log.Info<SitOnChairsFeature>(
                            $"微调：临时偏移 {_delta:+0;-0;0}（[ 往下 / ] 往上）→ 建议 {key} = " +
                            $"{ChairFactory.OffsetYFor(chair) + _delta:F0}；写进配置后重启生效");
                    }
                }
            }
        }

        [HarmonyPatch(typeof(DeviceManager), "SearchInteractDevice")]
        internal static class ChairDebugPatch
        {
            private static float _nextLogAt;

            private static void Postfix(DeviceManager __instance)
            {
                if (!Engine.Enabled<SitOnChairsFeature>())
                {
                    return;
                }
                if (!SitOnChairsFeature.Debug || Time.unscaledTime < _nextLogAt)
                {
                    return;
                }
                _nextLogAt = Time.unscaledTime + 2.5f;
                try
                {
                    MyPlayer my = Managers.Player != null ? Managers.Player.MyPlayer : null;
                    if (my == null || __instance == null || __instance.Cache == null)
                    {
                        return;
                    }
                    Vector3 pos = my.transform.position;
                    var sb = new StringBuilder();
                    int chairCount = 0;
                    int total = 0;
                    foreach (DeviceBase device in __instance.Cache.Values)
                    {
                        if (device == null)
                        {
                            continue;
                        }
                        total++;
                        if (!(device is Chair chair))
                        {
                            continue;
                        }
                        chairCount++;
                        Vector3 chairPos = chair.transform.position;
                        bool inRect = false;
                        List<RectInfo> rects = chair.Rects;
                        if (rects != null)
                        {
                            for (int i = 0; i < rects.Count; i++)
                            {
                                RectInfo r = rects[i];
                                float rx = chairPos.x + r.Pos.X;
                                float ry = chairPos.y + r.Pos.Y;
                                if (rx < pos.x && ry < pos.y && rx + r.Size.X > pos.x && ry + r.Size.Y >= pos.y)
                                {
                                    inRect = true;
                                    break;
                                }
                            }
                        }
                        var mr = chair.GetComponentInChildren<MeshRenderer>();
                        string render = mr == null ? "无MeshRenderer"
                            : $"{mr.sortingLayerName}/{mr.sortingOrder} 启用={mr.enabled}";
                        string kind = ChairFactory.IsSofa(chair) ? "沙发"
                            : !ChairFactory.IsCafe(chair) ? "长椅"
                            : SitOnChairsFeature.CafeSit ? "咖啡椅" : "咖啡椅·不可坐";
                        sb.Append($"\n    · [{kind}] 椅子@({chairPos.x:F0},{chairPos.y:F0}) 距离=" +
                                  $"{Vector2.Distance((Vector2)pos, (Vector2)chairPos):F0} 在框内={inRect} " +
                                  $"椅子状态={chair.DeviceState} 渲染={render}");
                    }
                    Log.Info<SitOnChairsFeature>(
                        $"调试：玩家@({pos.x:F0},{pos.y:F0}) 我的状态={my.State} " +
                        $"偏移=长椅{SitOnChairsFeature.SeatYOffset:F0}/沙发{SitOnChairsFeature.SofaYOffset:F0}/" +
                        $"咖啡椅{SitOnChairsFeature.CafeYOffset:F0} 咖啡椅可坐={SitOnChairsFeature.CafeSit} " +
                        $"设备总数={total} 椅子数={chairCount}{sb}");
                }
                catch (Exception ex)
                {
                    Log.Error<SitOnChairsFeature>(ex, "失败");
                }
            }
        }

        [HarmonyPatch(typeof(DeviceManager), "SearchInteractDevice")]
        internal static class ChairFurnitureOnTopPatch
        {
            private const float MatchRange = 60f;

            private static SpriteRenderer _furniture;

            private static int _savedOrder = int.MinValue;

            private static Chair _chair;

            private static int _lastFrame = -1;

            private static void Postfix(DeviceManager __instance)
            {
                if (!Engine.Enabled<SitOnChairsFeature>() || !SitOnChairsFeature.ChairOnTop)
                {
                    Restore();
                    return;
                }
                if (Time.frameCount == _lastFrame)
                {
                    return;
                }
                _lastFrame = Time.frameCount;

                MyPlayer my = Managers.Player != null ? Managers.Player.MyPlayer : null;
                if (my == null || my.State != EPlayerState.Sit)
                {
                    Restore();
                    return;
                }
                Chair chair = ChairFactory.NearestChair(__instance, my.transform.position);
                if (chair == null)
                {
                    Restore();
                }
                else if (chair != _chair)
                {
                    Restore();
                    _chair = chair;
                    RaiseFurniture(chair);
                }
            }

            private static void RaiseFurniture(Chair chair)
            {
                try
                {
                    DeviceInfo info = chair.Info;
                    if (info == null || info.Pos == null)
                    {
                        return;
                    }
                    Vector3 pos = new Vector3(info.Pos.X, info.Pos.Y, 0f);

                    SpriteRenderer[] all = UnityEngine.Object.FindObjectsByType<SpriteRenderer>(
                        FindObjectsSortMode.None);
                    float nearest = MatchRange;
                    foreach (SpriteRenderer sr in all)
                    {
                        if (sr != null && sr.enabled && sr.sortingOrder == 250)
                        {
                            float distance = Vector2.Distance((Vector2)sr.transform.position, (Vector2)pos);
                            if (distance < nearest)
                            {
                                nearest = distance;
                                _furniture = sr;
                            }
                        }
                    }
                    if (_furniture == null)
                    {
                        Log.Warn<SitOnChairsFeature>(
                            $"椅子置顶：没在 ({pos.x:F0},{pos.y:F0}) 附近找到家具美术");
                        return;
                    }
                    _savedOrder = _furniture.sortingOrder;
                    _furniture.sortingOrder = 255;
                    Log.Info<SitOnChairsFeature>(
                        $"椅子置顶：{_furniture.name} 抬到 {_furniture.sortingOrder}" +
                        $"（坐在 ({chair.transform.position.x:F0},{chair.transform.position.y:F0})）");
                }
                catch (Exception ex)
                {
                    Log.Error<SitOnChairsFeature>(ex, "椅子置顶失败");
                }
            }

            private static void Restore()
            {
                _chair = null;
                if (_furniture != null && _savedOrder != int.MinValue)
                {
                    _furniture.sortingOrder = _savedOrder;
                }
                _furniture = null;
                _savedOrder = int.MinValue;
            }

            internal static void EnsureRestored()
            {
                if (_furniture == null)
                {
                    return;
                }
                MyPlayer my = Managers.Player != null ? Managers.Player.MyPlayer : null;
                if (my == null || my.State != EPlayerState.Sit)
                {
                    Restore();
                }
            }
        }

        [HarmonyPatch(typeof(Managers), "Update")]
        internal static class ChairFurnitureOnTopSafetyPatch
        {
            private static void Postfix()
            {
                ChairFurnitureOnTopPatch.EnsureRestored();
            }
        }

        [HarmonyPatch(typeof(Managers), "Update")]
        internal static class LocalChairSpawnPatch
        {
            private static float _nextCheck;

            private static void Postfix()
            {
                if (Time.unscaledTime < _nextCheck)
                {
                    return;
                }
                _nextCheck = Time.unscaledTime + 1f;
                LocalChairSpawn.Check();
            }
        }

        [HarmonyPatch(typeof(VoiceManager), "EnqueueNormalChat")]
        internal static class SitSyncReceivePatch
        {
            [HarmonyPriority(Priority.First)]
            private static bool Prefix(int playerId, string message, bool isDeadByHost)
            {
                try
                {
                    if (!Engine.Enabled<SitOnChairsFeature>() || !SitOnChairsFeature.SyncSitting)
                    {
                        return true;
                    }
                    if (!SitSyncWire.IsWire(message))
                    {
                        return true;
                    }

                    if (Managers.Player != null && playerId == Managers.Player.MyPlayerID)
                    {
                        return false;
                    }

                    if (SitSyncWire.TryParse(message, out SitSyncMessage sync))
                    {
                        if (sync.Sit)
                        {
                            ApplyRemoteSit(playerId, sync.ChairId, sync.CharId);
                        }
                        else
                        {
                            ApplyRemoteRise(playerId, sync.ChairId);
                        }
                    }
                    return false;
                }
                catch (Exception ex)
                {
                    Log.Warn<SitOnChairsFeature>(ex, "坐姿同步接收异常");
                    return false;
                }
            }

            private static void ApplyRemoteSit(int playerId, int chairId, int charId)
            {
                Chair chair = FindChair(chairId);
                Player p = FindPlayer(playerId);
                if (p != null)
                {
                    SetPlayerStealth(p, true);
                }
                if (chair != null)
                {
                    chair.ChangeCharacter(charId, isInit: false);
                }
            }

            private static void ApplyRemoteRise(int playerId, int chairId)
            {
                Chair chair = FindChair(chairId);
                Player p = FindPlayer(playerId);
                if (p != null)
                {
                    SetPlayerStealth(p, false);
                }
                if (chair != null)
                {
                    chair.ChangeCharacter(0, isInit: true);
                }
            }

            private static Chair FindChair(int chairId)
            {
                if (Managers.Device?.Cache == null)
                {
                    return null;
                }
                foreach (DeviceBase device in Managers.Device.Cache.Values)
                {
                    if (device is Chair chair && chair.Info != null && chair.Info.DeviceId == chairId)
                    {
                        return chair;
                    }
                }
                return null;
            }

            private static Player FindPlayer(int playerId)
            {
                if (Managers.Player?.Players == null)
                {
                    return null;
                }
                return Managers.Player.Players.TryGetValue(playerId, out Player p) ? p : null;
            }

            private static void SetPlayerStealth(Player p, bool stealth)
            {
                try
                {
                    global::System.Reflection.MethodInfo m = AccessTools.Method(typeof(Player), "PlayerStealth");
                    m?.Invoke(p, new object[] { stealth });
                }
                catch (Exception ex)
                {
                    Log.Warn<SitOnChairsFeature>("远端玩家模型" + (stealth ? "隐藏" : "显示") + $"失败：{ex.Message}");
                }
            }
        }
    }
}
