using System;
using System.Collections.Generic;
using System.Reflection;
using Data;
using DT_Tools.Core;
using Protocol;
using Spine.Unity;
using UnityEngine;

namespace DT_Tools.Features.SitOnChairs
{
    internal static class ChairFactory
    {
        internal const string PrefabKey = "Chair";

        internal const string TextKey = "ChairInteract";

        internal const string TextValue = "坐下/起身";

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
                        Log.Info<SitOnChairsFeature>("已注入运行时 Chair 预制体");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error<SitOnChairsFeature>(ex, "注入预制体失败");
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
                Log.Error<SitOnChairsFeature>(ex, "设置渲染层级失败");
            }
        }

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
                Log.Error<SitOnChairsFeature>(ex, "应用坐姿偏移失败");
            }
        }

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
                        Log.Warn<SitOnChairsFeature>("找不到已加载的人物骨骼资源，椅子可能无法显示坐姿。");
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
                Log.Error<SitOnChairsFeature>(ex, "初始化骨架失败");
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

        internal static bool IsSofa(Chair chair)
        {
            try
            {
                string name = chair?.Data?.Name;
                if (string.IsNullOrEmpty(name))
                {
                    return false;
                }
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
