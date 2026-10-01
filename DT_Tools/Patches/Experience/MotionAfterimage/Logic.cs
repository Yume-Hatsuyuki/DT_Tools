using System.Collections.Generic;
using System.Linq;
using Data;
using DT_Tools.Core;
using Spine.Unity;
using UnityEngine;
using UnityEngine.Rendering;
using Protocol;

namespace DT_Tools.Patches.Experience.MotionAfterimage
{
    /// <summary>
    /// 活体残影：对齐 FlashVfx（ColorPlayerShadow + Idle 定格 + alpha 淡出）。
    /// 死亡定格：ShadowPlayerShadow 黑色最后一帧。
    /// Survive / Detective 地图内生效（可分别开关）。
    /// </summary>
    internal static class MotionAfterimageLogic
    {
        private static readonly string[] PrefabCandidates = { "ShadowPartnerVfx", "FlashVfx" };
        private const string ColorMatName = "ColorPlayerShadow";
        private const string DeathMatName = "ShadowPlayerShadow";

        private static readonly Color BlueTintRgb = new Color(0.35f, 0.65f, 1f, 1f);

        public static void Reset()
        {
            ClearAll();
            MotionAfterimageState.LoggedCreateFail = false;
            MotionAfterimageState.LoggedCreateOk = false;
        }

        public static void ClearAll()
        {
            foreach (KeyValuePair<int, AfterimageTrack> kv in MotionAfterimageState.Tracks.ToList())
                DestroyTrack(kv.Key);

            MotionAfterimageState.Tracks.Clear();

            if (MotionAfterimageState.Root != null)
            {
                Object.Destroy(MotionAfterimageState.Root.gameObject);
                MotionAfterimageState.Root = null;
            }
        }

        public static void Tick()
        {
            if (Managers.Game == null || Managers.Player == null)
                return;

            EGameState state = Managers.Game.State;
            bool inSurvive = state == EGameState.Survive && MotionAfterimageFeature.EnableInSurvive;
            bool inDetective = state == EGameState.Detective && MotionAfterimageFeature.EnableInDetective;

            if (!inSurvive && !inDetective)
            {
                // 阶段关闭或已离开地图：全清（含死亡定格）
                if (MotionAfterimageState.Tracks.Count > 0)
                    ClearAll();
                return;
            }

            // 调查阶段若关掉活体但仍可能要留死亡定格：上面 inDetective 已要求 EnableInDetective。
            // 若只关 ShowDeathResidual，见下方采样与 Despawn。

            EnsureRoot();
            float now = Time.time;
            float interval = Mathf.Max(0.05f, MotionAfterimageFeature.SpawnInterval);
            float fade = Mathf.Max(0.15f, MotionAfterimageFeature.FadeDuration);
            int maxLive = Mathf.Clamp(MotionAfterimageFeature.MaxPerPlayer, 1, 24);

            HashSet<int> liveIds = new HashSet<int>();
            int myId = Managers.Player.MyPlayer?.PublicInfo?.PlayerId ?? int.MinValue;
            bool selfAlive = Managers.Game.IsAlive;

            // 自己已死（幽灵）：禁止再刷活体残影，并立刻清掉残留的蓝色生前残影
            // 否则幽灵移动时会拖一串「生前形象」蓝影
            if (!selfAlive && myId != int.MinValue &&
                MotionAfterimageState.Tracks.TryGetValue(myId, out AfterimageTrack selfTrack))
            {
                ClearLiveOnly(selfTrack);
            }

            if (MotionAfterimageFeature.IncludeSelf && selfAlive && Managers.Player.MyPlayer != null)
            {
                SampleAndMaybeSpawn(Managers.Player.MyPlayer, now, interval, fade, maxLive);
                liveIds.Add(myId);
            }

            if (Managers.Player.Players != null)
            {
                foreach (Player p in Managers.Player.Players.Values)
                {
                    if (p == null || p.PublicInfo == null)
                        continue;
                    if (p.PublicInfo.PlayerId == myId)
                        continue;
                    // 他人若已是幽灵态，不刷活体蓝影（黑定格由 Despawn 负责）
                    if (p.PublicInfo.IsGhost)
                        continue;

                    SampleAndMaybeSpawn(p, now, interval, fade, maxLive);
                    liveIds.Add(p.PublicInfo.PlayerId);
                }
            }

            // 人又在场上（临时 Despawn 后又 Spawn）→ 清掉其死亡定格，只认「最后一次仍未回来的 Despawn」
            foreach (KeyValuePair<int, AfterimageTrack> kv in MotionAfterimageState.Tracks.ToList())
            {
                if (liveIds.Contains(kv.Key))
                {
                    if (kv.Value.DeathGhost != null)
                        DestroyDeathGhost(kv.Value);
                    continue;
                }

                if (kv.Value.DeathGhost != null)
                    continue;
                if (kv.Value.Live.Count == 0)
                    DestroyTrack(kv.Key);
            }

            if (!MotionAfterimageFeature.ShowDeathResidual)
            {
                foreach (KeyValuePair<int, AfterimageTrack> kv in MotionAfterimageState.Tracks.ToList())
                {
                    if (kv.Value.DeathGhost != null)
                        DestroyDeathGhost(kv.Value);
                }
            }

            foreach (KeyValuePair<int, AfterimageTrack> kv in MotionAfterimageState.Tracks.ToList())
                TickLiveFade(kv.Value, now);
        }

        /// <summary>
        /// 自己死亡（S_DEAD → Game.Dead）：
        /// 1) 必清自己的活体蓝影（避免幽灵拖着生前形象残影）；
        /// 2) 按配置定格黑色最后一帧。
        /// </summary>
        public static void OnLocalPlayerDead()
        {
            if (!Engine.Enabled<MotionAfterimageFeature>())
                return;

            MyPlayer me = Managers.Player?.MyPlayer;
            if (me == null || me.PublicInfo == null)
                return;

            int id = me.PublicInfo.PlayerId;
            if (!MotionAfterimageState.Tracks.TryGetValue(id, out AfterimageTrack track))
            {
                track = new AfterimageTrack();
                MotionAfterimageState.Tracks[id] = track;
            }

            // 无论是否留黑定格，先清掉蓝色活体残影
            ClearLiveOnly(track);

            if (me.CharData != null)
            {
                track.LastPos = me.transform != null ? me.transform.position : (UnityEngine.Vector3)me.Position;
                track.LastLookLeft = me.LookLeft;
                track.LastCharacterIdLive = me.CharData.DataId;
            }

            if (!MotionAfterimageFeature.ShowDeathResidual)
                return;
            if (!MotionAfterimageFeature.IncludeSelfDeathResidual)
                return;
            if (!IsMapPhaseActive())
                return;
            if (track.LastCharacterIdLive == 0)
                return;

            PlaceDeathResidual(track);
        }

        /// <summary>
        /// 只保留「最后一次 Despawn」的黑色定格：先删旧再新建。
        /// 无采样历史时从 PlayerCache 补全角色/坐标（幽灵视角下他人死亡也能定格）。
        /// 自己通常不走 Despawn（客户端忽略 S_DESPAWN self），见 OnLocalPlayerDead。
        /// </summary>
        public static void OnPlayerDespawned(int playerId)
        {
            if (!Engine.Enabled<MotionAfterimageFeature>())
                return;
            if (Managers.Game == null)
                return;
            if (!MotionAfterimageFeature.ShowDeathResidual)
                return;
            if (!IsMapPhaseActive())
                return;

            int myId = Managers.Player?.MyPlayer?.PublicInfo?.PlayerId ?? int.MinValue;
            if (playerId == myId && !MotionAfterimageFeature.IncludeSelfDeathResidual)
                return;

            if (!MotionAfterimageState.Tracks.TryGetValue(playerId, out AfterimageTrack track))
            {
                track = new AfterimageTrack();
                MotionAfterimageState.Tracks[playerId] = track;
            }

            ClearLiveOnly(track);
            TryFillTrackFromCache(playerId, track);

            if (track.LastCharacterIdLive == 0)
                return;

            PlaceDeathResidual(track);
        }

        /// <summary>Despawn 后 Players 已移除，从 Cache 取最后坐标与角色。</summary>
        private static void TryFillTrackFromCache(int playerId, AfterimageTrack track)
        {
            if (Managers.Player == null)
                return;

            Player cached = null;
            try
            {
                cached = Managers.Player.GetPlayerCache(playerId);
            }
            catch (global::System.Exception)
            {
                cached = null;
            }

            if (cached == null)
                return;

            if (cached.transform != null)
                track.LastPos = cached.transform.position;
            else if (cached.PublicInfo != null)
                track.LastPos = new Vector3(cached.Position.x, cached.Position.y, 0f);

            track.LastLookLeft = cached.LookLeft;

            if (cached.CharData != null)
                track.LastCharacterIdLive = cached.CharData.DataId;
            else if (cached.PublicInfo != null && cached.PublicInfo.CharacterId != 0)
                track.LastCharacterIdLive = cached.PublicInfo.CharacterId;
        }

        /// <summary>同一 track 只留一个黑色定格（先删再建）。</summary>
        private static void PlaceDeathResidual(AfterimageTrack track)
        {
            if (track.DeathGhost != null)
                DestroyDeathGhost(track);

            if (!TryCreateGhost(
                    track.LastCharacterIdLive,
                    track.LastPos,
                    track.LastLookLeft,
                    DeathMatName,
                    MotionAfterimageFeature.DeathSortingOrder,
                    isDeath: true,
                    out GameObject go,
                    out SkeletonAnimation anim,
                    out Material mat))
                return;

            if (anim?.Skeleton != null)
                anim.Skeleton.SetColor(Color.white);

            track.DeathGhost = go;
            track.DeathAnim = anim;
            track.DeathMat = mat;
        }

        private static bool IsMapPhaseActive()
        {
            if (Managers.Game == null)
                return false;
            EGameState state = Managers.Game.State;
            return (state == EGameState.Survive && MotionAfterimageFeature.EnableInSurvive)
                || (state == EGameState.Detective && MotionAfterimageFeature.EnableInDetective);
        }

        private static Color LiveStartColor()
        {
            float a = Mathf.Clamp01(MotionAfterimageFeature.LiveStartAlpha);
            if (MotionAfterimageFeature.UseBlueTint)
                return new Color(BlueTintRgb.r, BlueTintRgb.g, BlueTintRgb.b, a);
            return new Color(1f, 1f, 1f, a);
        }

        private static void SampleAndMaybeSpawn(Player p, float now, float interval, float fade, int maxLive)
        {
            if (p == null || p.CharData == null || p.PublicInfo == null)
                return;

            Vector3 worldPos = p.transform != null ? p.transform.position : (Vector3)p.Position;

            int id = p.PublicInfo.PlayerId;
            if (!MotionAfterimageState.Tracks.TryGetValue(id, out AfterimageTrack track))
            {
                track = new AfterimageTrack();
                MotionAfterimageState.Tracks[id] = track;
            }

            track.LastPos = worldPos;
            track.LastLookLeft = p.LookLeft;
            track.LastCharacterIdLive = p.CharData.DataId;

            // 已有死亡定格的人不再刷活体
            if (track.DeathGhost != null)
                return;

            if (now - track.LastSpawnTime < interval)
                return;

            track.LastSpawnTime = now;
            SpawnLive(track, p.CharData.DataId, worldPos, p.LookLeft, now, fade, maxLive);
        }

        private static void SpawnLive(
            AfterimageTrack track,
            int characterId,
            Vector3 pos,
            bool lookLeft,
            float now,
            float fade,
            int maxLive)
        {
            while (track.Live.Count >= maxLive)
            {
                AfterimageInstance oldest = track.Live[0];
                track.Live.RemoveAt(0);
                DestroyInstance(oldest);
            }

            if (!TryCreateGhost(
                    characterId,
                    pos,
                    lookLeft,
                    ColorMatName,
                    MotionAfterimageFeature.SortingOrder,
                    isDeath: false,
                    out GameObject go,
                    out SkeletonAnimation anim,
                    out Material mat))
                return;

            if (anim?.Skeleton != null)
                anim.Skeleton.SetColor(LiveStartColor());

            track.Live.Add(new AfterimageInstance
            {
                Go = go,
                Anim = anim,
                Mat = mat,
                BornTime = now,
                Lifetime = fade
            });
        }

        private static void TickLiveFade(AfterimageTrack track, float now)
        {
            Color start = LiveStartColor();
            for (int i = track.Live.Count - 1; i >= 0; i--)
            {
                AfterimageInstance inst = track.Live[i];
                float t = (now - inst.BornTime) / Mathf.Max(0.01f, inst.Lifetime);
                if (t >= 1f || inst.Go == null)
                {
                    track.Live.RemoveAt(i);
                    DestroyInstance(inst);
                    continue;
                }

                if (inst.Anim?.Skeleton != null)
                {
                    Color c = start;
                    c.a = start.a * (1f - t);
                    inst.Anim.Skeleton.SetColor(c);
                }
            }
        }

        private static void ClearLiveOnly(AfterimageTrack track)
        {
            for (int i = 0; i < track.Live.Count; i++)
                DestroyInstance(track.Live[i]);
            track.Live.Clear();
        }

        private static bool TryCreateGhost(
            int characterId,
            Vector3 pos,
            bool lookLeft,
            string matName,
            int sortingOrder,
            bool isDeath,
            out GameObject go,
            out SkeletonAnimation anim,
            out Material mat)
        {
            go = null;
            anim = null;
            mat = null;

            Transform parent = ResolveParent(preferStableRoot: isDeath);
            if (parent == null)
            {
                LogOnceFail("parent 为空（Root/DeviceRoot 均不可用）");
                return false;
            }

            CharacterData data = null;
            if (Managers.Data?.CharacterDic != null &&
                Managers.Data.CharacterDic.TryGetValue(characterId, out CharacterData cd))
                data = cd;

            if (data == null || string.IsNullOrEmpty(data.SkeletonPrefabName))
            {
                LogOnceFail($"角色数据无效 characterId={characterId}");
                return false;
            }

            go = InstantiatePrefab(parent);
            if (go == null)
            {
                LogOnceFail("Prefab 实例化失败（FlashVfx/ShadowPartnerVfx 均不可用）");
                return false;
            }

            FlashVfx flash = go.GetComponent<FlashVfx>();
            if (flash != null)
                flash.enabled = false;
            ShadowPartnerVfx partner = go.GetComponent<ShadowPartnerVfx>();
            if (partner != null)
                partner.enabled = false;

            anim = go.GetComponentInChildren<SkeletonAnimation>(true);
            if (anim == null)
            {
                LogOnceFail("Prefab 上找不到 SkeletonAnimation");
                Object.Destroy(go);
                go = null;
                return false;
            }

            try
            {
                SkeletonDataAsset sda = Managers.Resource.Load<SkeletonDataAsset>(data.SkeletonPrefabName);
                if (sda == null)
                    throw new global::System.Exception("SkeletonDataAsset 加载失败: " + data.SkeletonPrefabName);

                anim.skeletonDataAsset = sda;
                anim.Initialize(overwrite: true);

                if (anim.SkeletonDataAsset == null ||
                    anim.SkeletonDataAsset.atlasAssets == null ||
                    anim.SkeletonDataAsset.atlasAssets.Length == 0)
                    throw new global::System.Exception("SkeletonDataAsset.atlasAssets 为空");

                Material primary = anim.SkeletonDataAsset.atlasAssets[0].PrimaryMaterial;
                Material srcMat = Managers.Resource.Load<Material>(matName);
                if (srcMat == null)
                    throw new global::System.Exception("材质加载失败: " + matName);

                mat = Object.Instantiate(srcMat);
                mat.mainTexture = primary.mainTexture;
                anim.CustomMaterialOverride.Clear();
                anim.CustomMaterialOverride.Add(primary, mat);

                if (anim.Skeleton?.Data != null)
                {
                    Spine.Skin skin = anim.Skeleton.Data.FindSkin("1");
                    if (skin != null)
                    {
                        anim.Skeleton.SetSkin(skin);
                        anim.Skeleton.SetSlotsToSetupPose();
                    }
                }

                anim.AnimationState.SetAnimation(0, "1_Idle", loop: true);
                anim.Update(0f);
                anim.timeScale = 0f;
            }
            catch (global::System.Exception ex)
            {
                LogOnceFail("创建异常: " + ex.Message);
                if (mat != null)
                {
                    Object.Destroy(mat);
                    mat = null;
                }
                Object.Destroy(go);
                go = null;
                anim = null;
                return false;
            }

            foreach (SortingGroup sg in go.GetComponentsInChildren<SortingGroup>(true))
                sg.sortingOrder = sortingOrder;

            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                r.sortingOrder = sortingOrder;
                r.enabled = true;
            }

            go.transform.position = pos;
            ApplyFacing(go, lookLeft);
            go.SetActive(true);

            if (!MotionAfterimageState.LoggedCreateOk)
            {
                MotionAfterimageState.LoggedCreateOk = true;
                Log.Info<MotionAfterimageFeature>(
                    $"残影创建成功 characterId={characterId} mat={matName} death={isDeath} pos={pos}");
            }

            return true;
        }

        private static GameObject InstantiatePrefab(Transform parent)
        {
            foreach (string key in PrefabCandidates)
            {
                try
                {
                    GameObject go = Managers.Resource.Instantiate(key, parent);
                    if (go != null)
                        return go;
                }
                catch (global::System.Exception ex)
                {
                    Log.Debug<MotionAfterimageFeature>($"Instantiate {key} 异常: {ex.Message}");
                }
            }

            return null;
        }

        private static Transform ResolveParent(bool preferStableRoot = false)
        {
            // 死亡定格挂到自有 Root，避免幽灵阶段 Device 显隐影响
            if (preferStableRoot)
            {
                EnsureRoot();
                return MotionAfterimageState.Root;
            }

            if (Managers.Device != null && Managers.Device.DeviceRoot != null)
                return Managers.Device.DeviceRoot;

            EnsureRoot();
            return MotionAfterimageState.Root;
        }

        private static void ApplyFacing(GameObject go, bool lookLeft)
        {
            if (go == null)
                return;
            Vector3 scale = go.transform.localScale;
            float ax = Mathf.Abs(scale.x);
            if (ax < 0.01f)
                ax = 1f;
            // 对齐 FlashVfx：lookLeft 时 x 为正
            scale.x = lookLeft ? ax : -ax;
            go.transform.localScale = scale;
        }

        private static void DestroyInstance(AfterimageInstance inst)
        {
            if (inst == null)
                return;
            if (inst.Go != null)
            {
                if (Managers.Resource != null)
                    Managers.Resource.Destroy(inst.Go);
                else
                    Object.Destroy(inst.Go);
            }
            // 材质副本是 Instantiate 出来的运行时对象,必须随实例销毁,否则等场景切换才释放
            if (inst.Mat != null)
                Object.Destroy(inst.Mat);
            inst.Go = null;
            inst.Anim = null;
            inst.Mat = null;
        }

        private static void DestroyDeathGhost(AfterimageTrack track)
        {
            if (track.DeathGhost != null)
            {
                if (Managers.Resource != null)
                    Managers.Resource.Destroy(track.DeathGhost);
                else
                    Object.Destroy(track.DeathGhost);
                track.DeathGhost = null;
                track.DeathAnim = null;
            }
            if (track.DeathMat != null)
            {
                Object.Destroy(track.DeathMat);
                track.DeathMat = null;
            }
        }

        private static void DestroyTrack(int playerId)
        {
            if (!MotionAfterimageState.Tracks.TryGetValue(playerId, out AfterimageTrack track))
                return;
            ClearLiveOnly(track);
            DestroyDeathGhost(track);
            MotionAfterimageState.Tracks.Remove(playerId);
        }

        private static void EnsureRoot()
        {
            if (MotionAfterimageState.Root != null)
                return;

            GameObject root = new GameObject("DT_MotionAfterimageRoot");
            Object.DontDestroyOnLoad(root);
            MotionAfterimageState.Root = root.transform;
        }

        private static void LogOnceFail(string reason)
        {
            if (MotionAfterimageState.LoggedCreateFail)
                return;
            MotionAfterimageState.LoggedCreateFail = true;
            Log.Warn<MotionAfterimageFeature>("残影创建失败（仅报一次）: " + reason);
        }
    }
}
