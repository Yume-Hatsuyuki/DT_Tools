using System;
using System.Collections.Generic;
using System.Text;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;
using Spine.Unity;
using UnityEngine;

namespace DT_Tools.Patches.Fun.SitOnChairs
{
    /// <summary>
    /// 可坐椅子补丁集合（原 IslandMap v1.5.2 移植）：
    /// 1. LoadMapSet：功能开启时把原版主图（School）加载重定向为岛屿数据（注入 7 份内嵌数据）；
    /// 2. Spawn/SetInfo/GetText：保证运行时 Chair 预制体存在、坐姿人物层级/偏移正确、交互文案可用；
    /// 3. GetInteractMessage/Interact：等待室咖啡椅默认不可坐（CafeSit=false 时提示为空、按 E 无效），
    ///    CafeSit=true 时在大厅本地坐下（不发网络包）；
    /// 4. UpdateAnimation/FixedUpdateMove：坐着时暂停玩家动画与移动逻辑（本地坐姿用）；
    /// 5. SearchInteractDevice（×3）：坐着时 [ / ] 实时微调坐姿高低、可选调试日志、椅子美术置顶；
    /// 6. Managers.Update：确保非坐着状态时把抬高的椅子美术还原。
    /// 所有补丁均以 Engine.Enabled&lt;SitOnChairsFeature&gt; 为总开关，关闭即完全原版行为。
    /// </summary>
    internal static class Patches
    {
        // ===== 1. 地图数据注入 + 加载重定向 =====

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
                    // 只重定向原版主图（School）；Island/Waiting/Tutorial 不受影响
                    if (type != EMapType.School)
                    {
                        return;
                    }
                    if (IslandData.Inject())
                    {
                        type = EMapType.Island;
                        Log.Info<SitOnChairsFeature>("[可坐椅子] 已将 LoadMapSet(School) 重定向为 Island（原版数据+椅子条目）。");
                    }
                    else
                    {
                        Log.Error<SitOnChairsFeature>("[可坐椅子] 岛屿数据注入失败，本次沿用原版学校地图。");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error<SitOnChairsFeature>("[可坐椅子] 地图重定向异常，沿用原版：" + ex.Message);
                }
            }
        }

        // ===== 2. 椅子预制体与坐姿 =====

        /// <summary>DeviceManager.Spawn(string, DeviceInfo)（0.1.16b DeviceManager.cs:155）：spawn 椅子时确保运行时预制体存在。</summary>
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

        /// <summary>Chair.SetInfo（0.1.16b Chair.cs，Chair 继承 DeviceBase.SetInfo DeviceBase.cs:132）：初始化骨架、固定层级、应用坐姿偏移。</summary>
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

        /// <summary>Managers.GetText（0.1.16b Managers.cs:223）：补 "ChairInteract" 交互文案（原版文案表无此键）。</summary>
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

        // ===== 3. 咖啡椅不可坐 / 本地坐 =====

        /// <summary>Chair.GetInteractMessage（0.1.16b Chair.cs）：CafeSit=false 时咖啡椅交互提示置空（按 E 无效、不显示提示）。</summary>
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

        /// <summary>Chair.GetInteractMessage Postfix：本地坐着时提示改为"起身"。</summary>
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

        /// <summary>Chair.Interact（0.1.16b Chair.cs）：Lobby 阶段且 CafeSit=true 时改走本地坐
        /// （不发送 C_INTERACT_CHAIR）；对局内（非 Lobby）本地立即坐（GameSit），
        /// 同时照发原版 C_INTERACT_CHAIR（房主装了则全房同步，没装也不影响本地坐）。</summary>
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
                // 对局内（调查/生存等）：本地立即坐/起身，原版网络包照发（return true）
                if (Managers.Game.State == EGameState.Survive || Managers.Game.State == EGameState.Detective)
                {
                    GameSit.Toggle(__instance);
                }
                return true;
            }
        }

        // ===== 4. 本地坐姿时暂停玩家动画/移动 =====

        /// <summary>Player.UpdateAnimation（0.1.16b Player.cs）：本地坐着（等待室/对局内）时跳过玩家动画更新。</summary>
        [HarmonyPatch(typeof(Player), "UpdateAnimation")]
        internal static class LocalSitAnimPatch
        {
            private static bool Prefix()
            {
                return !LocalSit.IsSitting && !GameSit.IsSitting;
            }
        }

        /// <summary>MyPlayer.FixedUpdateMove（0.1.16b MyPlayer.cs:1869）：本地坐着时钉住位置、跳过移动。</summary>
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

        // ===== 5. 微调 / 调试 / 椅子美术置顶 =====

        /// <summary>DeviceManager.SearchInteractDevice（0.1.16b DeviceManager.cs:686）Postfix：[ / ] 实时微调坐姿高低（每按一次 5 世界单位）。</summary>
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
                            $"[可坐椅子微调] 临时偏移 {_delta:+0;-0;0}（[ 往下 / ] 往上）→ 建议 {key} = " +
                            $"{ChairFactory.OffsetYFor(chair) + _delta:F0}；写进配置后重启生效");
                    }
                }
            }
        }

        /// <summary>DeviceManager.SearchInteractDevice Postfix：Debug=true 时每 2.5 秒输出椅子调试日志。</summary>
        [HarmonyPatch(typeof(DeviceManager), "SearchInteractDevice")]
        internal static class ChairDebugPatch
        {
            private static float _nextLogAt;

            private static void Postfix(DeviceManager __instance)
            {
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
                        $"[可坐椅子调试] 玩家@({pos.x:F0},{pos.y:F0}) 我的状态={my.State} " +
                        $"偏移=长椅{SitOnChairsFeature.SeatYOffset:F0}/沙发{SitOnChairsFeature.SofaYOffset:F0}/" +
                        $"咖啡椅{SitOnChairsFeature.CafeYOffset:F0} 咖啡椅可坐={SitOnChairsFeature.CafeSit} " +
                        $"设备总数={total} 椅子数={chairCount}{sb}");
                }
                catch (Exception ex)
                {
                    Log.Error<SitOnChairsFeature>("[可坐椅子调试] 失败：" + ex.Message);
                }
            }
        }

        /// <summary>DeviceManager.SearchInteractDevice Postfix：ChairOnTop=true 且坐着时，把椅子正前方的家具美术抬到 255 层级。</summary>
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
                            $"[可坐椅子] 椅子置顶：没在 ({pos.x:F0},{pos.y:F0}) 附近找到家具美术");
                        return;
                    }
                    _savedOrder = _furniture.sortingOrder;
                    _furniture.sortingOrder = 255;
                    Log.Info<SitOnChairsFeature>(
                        $"[可坐椅子] 椅子置顶：{_furniture.name} 抬到 {_furniture.sortingOrder}" +
                        $"（坐在 ({chair.transform.position.x:F0},{chair.transform.position.y:F0})）");
                }
                catch (Exception ex)
                {
                    Log.Error<SitOnChairsFeature>("[可坐椅子] 椅子置顶失败：" + ex.Message);
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

        /// <summary>Managers.Update（0.1.16b Managers.cs:325）Postfix：非坐着状态时还原被抬高的家具美术（安全兜底）。</summary>
        [HarmonyPatch(typeof(Managers), "Update")]
        internal static class ChairFurnitureOnTopSafetyPatch
        {
            private static void Postfix()
            {
                ChairFurnitureOnTopPatch.EnsureRestored();
            }
        }
    }
}
