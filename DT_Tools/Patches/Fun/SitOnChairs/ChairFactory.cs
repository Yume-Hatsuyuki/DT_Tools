using System;
using System.Collections.Generic;
using System.Reflection;
using Data;
using DT_Tools.Core;
using Protocol;
using Spine.Unity;
using UnityEngine;

namespace DT_Tools.Patches.Fun.SitOnChairs
{
    /// <summary>
    /// 椅子运行时工厂（原 IslandMap ChairFactory）：游戏原版资源里没有 "Chair"
    /// 预制体（0.1.16b 只有 Chair.cs 逻辑），这里在运行时动态构建一个：
    /// 根物体 = 透明 SpriteRenderer（sortingOrder 240，仅承载设备交互与定位），
    /// 子物体 Skeleton = MeshRenderer（sortingOrder 260 = 坐姿人物层级）+ SkeletonAnimation，
    /// 骨架数据取任意已加载的角色骨骼资源（FindSkeletonData），坐下时 Chair.ChangeCharacter
    /// 会按坐的人的角色换骨骼。同时负责坐姿偏移（椅子设备整体移动到 数据坐标+偏移）
    /// 与坐姿人物渲染层级设置。
    /// </summary>
    internal static class ChairFactory
    {
        /// <summary>注入到 ResourceManager._resources 的预制体键名（0.1.16b 无此资源，运行时注入）</summary>
        internal const string PrefabKey = "Chair";

        internal const string TextKey = "ChairInteract";

        internal const string TextValue = "坐下/起身";

        /// <summary>坐姿人物渲染层级：高于地图 250、低于其他玩家，见 README/ChairFactory 说明</summary>
        internal const int CharacterSortingOrder = 260;

        private static GameObject _template;

        private static FieldInfo _resField;

        private static bool _loggedNoSkeleton;

        private static Dictionary<string, UnityEngine.Object> ResourceDict()
        {
            if (_resField == null)
            {
                _resField = typeof(ResourceManager).GetField("_resources", BindingFlags.Instance | BindingFlags.NonPublic);
            }
            ResourceManager resource = Managers.Resource;
            if (resource == null || _resField == null)
            {
                return null;
            }
            return _resField.GetValue(resource) as Dictionary<string, UnityEngine.Object>;
        }

        /// <summary>确保 "Chair" 预制体存在于资源字典（不存在则构建并注入）。</summary>
        internal static void Ensure()
        {
            try
            {
                var dictionary = ResourceDict();
                if (dictionary != null && (!dictionary.TryGetValue(PrefabKey, out var value) || value == null))
                {
                    if (_template == null)
                    {
                        _template = Build();
                    }
                    if (_template != null)
                    {
                        dictionary[PrefabKey] = _template;
                        Log.Info<SitOnChairsFeature>("[可坐椅子] 已注入运行时 Chair 预制体");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error<SitOnChairsFeature>("[可坐椅子] 注入预制体失败：" + ex.Message);
            }
        }

        private static GameObject Build()
        {
            var root = new GameObject("Chair");
            UnityEngine.Object.DontDestroyOnLoad(root);

            var sprite = root.AddComponent<SpriteRenderer>();
            sprite.sprite = TransparentSprite();
            sprite.sortingOrder = 240;

            var skeleton = new GameObject("Skeleton");
            skeleton.transform.SetParent(root.transform, false);
            skeleton.AddComponent<MeshFilter>();
            var meshRenderer = skeleton.AddComponent<MeshRenderer>();
            meshRenderer.sortingLayerName = "Default";
            meshRenderer.sortingOrder = CharacterSortingOrder;
            EnsureSkeleton(skeleton.AddComponent<SkeletonAnimation>());
            return root;
        }

        /// <summary>把椅子上的坐姿人物 MeshRenderer 固定到 260 层级并启用。</summary>
        internal static void ApplyCharacterSorting(Component chair)
        {
            if (chair == null)
            {
                return;
            }
            try
            {
                var meshRenderer = chair.GetComponentInChildren<MeshRenderer>();
                if (meshRenderer != null)
                {
                    meshRenderer.sortingLayerName = "Default";
                    meshRenderer.sortingOrder = CharacterSortingOrder;
                    meshRenderer.enabled = true;
                }
            }
            catch (Exception ex)
            {
                Log.Error<SitOnChairsFeature>("[可坐椅子] 设置渲染层级失败：" + ex.Message);
            }
        }

        /// <summary>把椅子设备整体移到 数据坐标 + 坐姿偏移（人跟着椅子动）。</summary>
        internal static void ApplySeatOffset(Chair chair)
        {
            if (chair == null)
            {
                return;
            }
            try
            {
                DeviceInfo info = chair.Info;
                if (info != null && info.Pos != null)
                {
                    chair.transform.position = new Vector3(
                        info.Pos.X + SitOnChairsFeature.SeatXOffset,
                        info.Pos.Y + OffsetYFor(chair),
                        0f);
                }
            }
            catch (Exception ex)
            {
                Log.Error<SitOnChairsFeature>("[可坐椅子] 应用坐姿偏移失败：" + ex.Message);
            }
        }

        /// <summary>初始化椅子的 SkeletonAnimation（防空引用；骨架数据取任意已加载角色骨骼）。</summary>
        internal static void EnsureSkeleton(SkeletonAnimation anim)
        {
            if (anim == null)
            {
                return;
            }
            try
            {
                var meshRenderer = anim.GetComponent<MeshRenderer>();
                if (meshRenderer != null)
                {
                    meshRenderer.sortingLayerName = "Default";
                    meshRenderer.sortingOrder = CharacterSortingOrder;
                }
                if (anim.Skeleton != null)
                {
                    return;
                }
                SkeletonDataAsset dataAsset = FindSkeletonData();
                if (dataAsset == null)
                {
                    if (!_loggedNoSkeleton)
                    {
                        _loggedNoSkeleton = true;
                        Log.Warn<SitOnChairsFeature>("[可坐椅子] 找不到已加载的人物骨骼资源，椅子可能无法显示坐姿。");
                    }
                    return;
                }
                anim.skeletonDataAsset = dataAsset;
                anim.Initialize(true, false);
                if (anim.Skeleton != null)
                {
                    anim.Skeleton.SetColor(Color.clear);
                }
                _loggedNoSkeleton = false;
            }
            catch (Exception ex)
            {
                Log.Error<SitOnChairsFeature>("[可坐椅子] 初始化骨架失败：" + ex.Message);
            }
        }

        private static SkeletonDataAsset FindSkeletonData()
        {
            DataManager data = Managers.Data;
            if (data == null || data.CharacterDic == null)
            {
                return null;
            }
            foreach (CharacterData value in data.CharacterDic.Values)
            {
                if (value != null && !string.IsNullOrEmpty(value.SkeletonPrefabName))
                {
                    SkeletonDataAsset asset = Managers.Resource.Load<SkeletonDataAsset>(value.SkeletonPrefabName);
                    if (asset != null)
                    {
                        return asset;
                    }
                }
            }
            return null;
        }

        private static Sprite TransparentSprite()
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.SetPixels(new[] { Color.clear, Color.clear, Color.clear, Color.clear });
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f), 100f);
        }

        // ---------- 椅子种类与偏移 ----------

        internal static bool IsSofa(Chair chair)
        {
            try
            {
                string name = chair?.Data?.Name;
                if (string.IsNullOrEmpty(name))
                {
                    return false;
                }
                // 数据名可能是中文（如"炼金室沙发"）或英文（"Sofa"/"Couch"），都算沙发
                return name.IndexOf("Sofa", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("Couch", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("沙发", StringComparison.Ordinal) >= 0;
            }
            catch
            {
                return false;
            }
        }

        internal static bool IsCafe(Chair chair)
        {
            try
            {
                string name = chair?.Data?.Name;
                return !string.IsNullOrEmpty(name) && name.IndexOf("Cafe", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch
            {
                return false;
            }
        }

        internal static float OffsetYFor(Chair chair)
        {
            if (IsSofa(chair))
            {
                return SitOnChairsFeature.SofaYOffset;
            }
            if (IsCafe(chair))
            {
                return SitOnChairsFeature.CafeYOffset;
            }
            return SitOnChairsFeature.SeatYOffset;
        }

        /// <summary>找离指定位置最近的椅子（500 世界单位内）。</summary>
        internal static Chair NearestChair(DeviceManager dm, Vector3 pos)
        {
            if (dm == null || dm.Cache == null)
            {
                return null;
            }
            Chair result = null;
            float nearest = 500f;
            foreach (DeviceBase device in dm.Cache.Values)
            {
                if (device is Chair chair)
                {
                    float distance = Vector2.Distance((Vector2)pos, (Vector2)chair.transform.position);
                    if (distance < nearest)
                    {
                        nearest = distance;
                        result = chair;
                    }
                }
            }
            return result;
        }
    }
}
