using System;
using System.Collections.Generic;
using System.Reflection;
using Protocol;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace DT_Tools.Patches.Experience.CharacterMapPin
{
    /// <summary>
    /// 界面附加：pin/箭头的角色 Map 头像解析与替换、常驻箭头对象的构建与销毁、
    /// Enabled 热关闭时把已替换 pin 还原为原版贴图。
    ///
    /// Pin 替换不调用 pin.TurnComplyRules()——它会把 AlreadyComplyRules=true（0.1.15b
    /// UI_MinimapSubItem.cs:71-75），导致 Kaho 的 RefreshComplyRulesPin 跳过
    /// SetComplyRulesArrow，监视箭头直接消失。只换贴图，不碰 AlreadyComplyRules。
    /// 官方 Black/White 主要差边框色（粉/绿）；Kaho 路径固定用 Black。
    /// </summary>
    internal static class CharacterMapPinUi
    {
        // ── UI 元素魔法索引（UI_Base 的 protected GetObject/GetImage，0.1.15b UI_Base.cs:93/:113，
        //    本类不在继承链上，经下方缓存的开放实例委托调用）──
        /// <summary>UI_MinimapSubItem 的 GameObjects 枚举 ComplyRules=0（0.1.15b UI_MinimapSubItem.cs:7-10）。</summary>
        private const int PinComplyRulesObject = 0;
        /// <summary>UI_MinimapSubItem 的 Images 枚举 Target=0（0.1.15b UI_MinimapSubItem.cs:12-15）。</summary>
        private const int PinMarkImage = 0;
        /// <summary>UI_Arrow 的 Images 枚举 Arrow=0 / Mark=1（0.1.15b UI_Arrow.cs:6-9）。</summary>
        private const int ArrowMarkImage = 1;

        private const string WhiteKey = "_Map_White.sprite";
        private const string BlackKey = "_Map_Black.sprite";

        private static bool _diagnosticsLogged;

        // ── 反射缓存（pin 贴图替换/还原逐帧调用；HarmonyX 的 Traverse 无 MethodInfo 重载，
        //    且 Traverse 绑定目标实例、跨实例不能复用，故缓存开放实例委托 / PropertyInfo）──
        /// <summary>UI_Base.GetObject(int) protected：0.1.15b UI_Base.cs:93。</summary>
        private static readonly Func<UI_Base, int, GameObject> PinGetObject =
            Bind<Func<UI_Base, int, GameObject>>(typeof(UI_Base), "GetObject", new[] { typeof(int) });

        /// <summary>UI_Base.GetImage(int) protected：0.1.15b UI_Base.cs:113。</summary>
        private static readonly Func<UI_Base, int, Image> PinGetImage =
            Bind<Func<UI_Base, int, Image>>(typeof(UI_Base), "GetImage", new[] { typeof(int) });

        /// <summary>UI_Arrow.TargetPos { get; private set; }：0.1.15b UI_Arrow.cs:20，每帧反射写入。</summary>
        private static readonly PropertyInfo ArrowTargetPos = AccessTools.Property(typeof(UI_Arrow), "TargetPos");

        /// <summary>按名取实例方法并绑定为开放实例委托（游戏方法缺失时返回 null，调用点跳过）。</summary>
        private static T Bind<T>(Type owner, string name, Type[] parameters) where T : Delegate
        {
            MethodInfo mi = AccessTools.Method(owner, name, parameters);
            return mi == null ? null : (T)Delegate.CreateDelegate(typeof(T), null, mi);
        }

        // ── 贴图键与解析 ──────────────────────────────────────────────

        public static bool IsKnownBlack(int playerId)
            => Managers.Player.KnownBlackIds.Contains(playerId);

        /// <summary>角色 + 已识破黑幕用黑版头像，其余用白版（与 Kaho 箭头同源键）。</summary>
        public static string BuildKey(Player player)
            => player.CharData.Type + (IsKnownBlack(player.PublicInfo.PlayerId) ? BlackKey : WhiteKey);

        /// <summary>
        /// 只查预加载缓存。顺序：key → 同色 Black（若原是 White）→ Someone 对应色。
        /// 绝不发起 Addressables 加载（Managers.Resource.Load 直查缓存，0.1.15b ResourceManager.cs:69）。
        /// </summary>
        public static Sprite GetSprite(string key)
        {
            Sprite sprite = Managers.Resource.Load<Sprite>(key);
            if (sprite == null && key.EndsWith(WhiteKey))
                sprite = Managers.Resource.Load<Sprite>(key.Replace(WhiteKey, BlackKey));
            if (sprite == null)
            {
                if (key.EndsWith(WhiteKey))
                    sprite = Managers.Resource.Load<Sprite>("Someone" + WhiteKey);
                if (sprite == null)
                    sprite = Managers.Resource.Load<Sprite>("Someone" + BlackKey);
            }
            return sprite;
        }

        // ── pin 贴图替换 ──────────────────────────────────────────────

        public static void ApplyPin(UI_MinimapSubItem pin, Player player, bool replaceBlack)
        {
            if (pin.Type != Define.EMinimapPinType.Player)
                return;

            bool isBlack = IsKnownBlack(player.PublicInfo.PlayerId);
            if (isBlack && !replaceBlack)
                return;

            string key = isBlack
                ? player.CharData.Type + BlackKey
                : player.CharData.Type + WhiteKey;
            Sprite sprite = GetSprite(key);
            if (sprite == null)
                return;

            // ComplyRules 子物体：SetVisible 用游戏扩展（0.1.15b Extension.cs:92）
            GameObject comply = PinGetObject?.Invoke(pin, PinComplyRulesObject);
            if (comply != null)
                comply.SetVisible(true);

            Image mark = PinGetImage?.Invoke(pin, PinMarkImage);
            if (mark != null)
                mark.sprite = sprite;

            if (!_diagnosticsLogged)
            {
                _diagnosticsLogged = true;
                Log.Debug<CharacterMapPinFeature>(
                    $"首次应用: 角色={player.CharData.Type}, isBlack={isBlack}, sprite={sprite.name}");
            }
        }

        // ── 常驻角色箭头（不开平板也可见）──────────────────────────────
        // 用 WeaponArrow 类型 + 每帧更新 TargetPos（private set 经缓存的 PropertyInfo 写入）。
        // Mark 头像（GetImage(1)）替换为角色 *_Map_Black/White，与 Kaho 箭头一致。

        public static ArrowState EnsureArrow(MyPlayer my, Player player)
        {
            int id = player.PublicInfo.PlayerId;
            if (CharacterMapPinState.Arrows.TryGetValue(id, out ArrowState state)
                && state.Arrow != null)
                return state;

            if (state != null)
                CharacterMapPinState.Arrows.Remove(id);

            UI_Arrow arrow = Managers.UI.MakeWorldSpaceUI<UI_Arrow>(my.UIGroup);  // Player.UIGroup: 0.1.15b Player.cs:333
            if (arrow == null)
                return null;

            arrow.transform.localPosition = new Vector2(0f, 90f);
            arrow.SetInfo(EArrowType.WeaponArrow, player.Position);

            // 覆盖轨道半径为 Kaho 角色箭头的 300（_orbitRadius：0.1.15b UI_Arrow.cs:14）
            Traverse.Create(arrow).Field("_orbitRadius")
                .SetValue(CharacterMapPinState.CharacterArrowOrbit);

            Image mark = Traverse.Create(arrow).Method("GetImage", ArrowMarkImage).GetValue<Image>();

            state = new ArrowState
            {
                Arrow = arrow,
                Mark = mark,
                SpriteKey = null
            };
            CharacterMapPinState.Arrows[id] = state;
            return state;
        }

        public static void UpdateArrow(ArrowState state, Player player)
        {
            // TargetPos 是 { get; private set; }：0.1.15b UI_Arrow.cs:20，经缓存的 PropertyInfo 反射写入
            ArrowTargetPos?.SetValue(state.Arrow, player.Position);

            string key = BuildKey(player);
            if (state.SpriteKey == key)
                return;

            Sprite sprite = GetSprite(key);
            if (sprite != null && state.Mark != null)
            {
                state.Mark.sprite = sprite;
                state.SpriteKey = key;
            }
        }

        public static void RemoveStaleArrows(HashSet<int> liveIds)
        {
            if (CharacterMapPinState.Arrows.Count == 0)
                return;

            List<int> remove = null;
            foreach (KeyValuePair<int, ArrowState> pair in CharacterMapPinState.Arrows)
            {
                bool stale = !liveIds.Contains(pair.Key) || pair.Value.Arrow == null;
                if (!stale)
                    continue;

                if (pair.Value.Arrow != null)
                    Managers.Resource.Destroy(pair.Value.Arrow.gameObject);
                (remove ??= new List<int>()).Add(pair.Key);
            }

            if (remove != null)
            {
                foreach (int id in remove)
                    CharacterMapPinState.Arrows.Remove(id);
            }
        }

        public static void ClearAllArrows()
        {
            if (CharacterMapPinState.Arrows.Count == 0)
                return;

            foreach (ArrowState state in CharacterMapPinState.Arrows.Values)
            {
                if (state.Arrow != null)
                    Managers.Resource.Destroy(state.Arrow.gameObject);
            }
            CharacterMapPinState.Arrows.Clear();
        }

        // ── pin 贴图还原（Enabled 热关闭）──────────────────────────────
        // 原版 RefreshPlayerPin 对已存在的 pin 只调 SetLocalPosition、不重设 sprite
        //（0.1.15b UI_GameScene.cs:850-866 / UI_GameTablet.cs:1211-1226 的 else 分支已核实），
        // 所以热关闭后本功能换上的角色头像不会自行还原，必须在 OnDisabled 主动恢复。

        /// <summary>
        /// 遍历 HUD（UI_GameScene._playerPinList）与平板（UI_GameTablet._subItems）两份 pin 列表，
        /// 把本功能替换过的 Type==Player pin 还原为原版贴图。
        /// 本功能只动过 Type==Player 的 pin（ApplyPin 的入口过滤），其余类型原样。
        /// </summary>
        public static void RestoreAllPins()
        {
            if (Managers.Resource == null)
                return;

            // 实例获取：场景 UI 经 UIManager.GetSceneUI<T>（0.1.15b UIManager.cs:311，
            // 返回 _sceneUI as T）；平板经 TabletManager.Tablet（0.1.15b TabletManager.cs:14，
            // Managers.Tablet 静态入口 0.1.15b Managers.cs:90）——平板不是 SceneUI，不能走 GetSceneUI
            UI_GameScene scene = Managers.UI != null ? Managers.UI.GetSceneUI<UI_GameScene>() : null;
            if (scene != null)
                RestorePinsOf(CharacterMapPinLogic.HudPinListOf(scene));

            UI_GameTablet tablet = Managers.Tablet != null ? Managers.Tablet.Tablet : null;
            if (tablet != null)
                RestorePinsOf(CharacterMapPinLogic.TabletPinListOf(tablet));
        }

        private static void RestorePinsOf(List<UI_MinimapSubItem> pins)
        {
            if (pins == null)
                return;

            foreach (UI_MinimapSubItem pin in pins)
                RestorePin(pin);
        }

        /// <summary>
        /// 单个 pin 还原：根底图按原版 RefreshType 的取图逻辑走 UI_MinimapSubItem.PinSpriteKey
        ///（public static，0.1.15b UI_MinimapSubItem.cs:85；Player → "minimap_player.sprite"）。
        /// 黑幕 pin 语义特殊：已识破黑方的原版表现是 TurnBlack 的 minimap_black.sprite（:62-67），
        /// 不在 PinSpriteKey 表内——按 PinSpriteKey 的原版逻辑统一还原为通用玩家底图即可，
        /// 下一次识破事件触发时由原版 RefreshBlackPin 重新置黑；不调 sizeDelta（ApplyPin 替换时也没改尺寸）。
        /// ComplyRules 子物体：ApplyPin 曾强制 SetVisible(true)，还原时——
        /// 未识破态（AlreadyComplyRules=false，:39）按原版 SetInfo 默认隐藏（:57）；
        /// 已识破态原版本就保持可见（TurnComplyRules :71-81），贴图恢复为原版所用的 &lt;角色&gt;_Map_Black。
        /// </summary>
        private static void RestorePin(UI_MinimapSubItem pin)
        {
            if (pin == null || pin.Type != Define.EMinimapPinType.Player)
                return;

            Sprite vanilla = Managers.Resource.Load<Sprite>(UI_MinimapSubItem.PinSpriteKey(pin.Type));
            if (vanilla != null)
            {
                Image dot = pin.GetComponent<Image>();   // 原版 RefreshType/TurnBlack 写的根 Image
                if (dot != null)
                    dot.sprite = vanilla;
            }

            GameObject comply = PinGetObject?.Invoke(pin, PinComplyRulesObject);
            if (comply == null)
                return;

            if (!pin.AlreadyComplyRules)
            {
                comply.SetVisible(false);   // 游戏扩展（0.1.15b Extension.cs:92）
                return;
            }

            // 已识破玩家：原版 TurnComplyRules 固定用 Black 版头像（0.1.15b UI_MinimapSubItem.cs:79）
            Player cached = Managers.Player != null ? Managers.Player.GetPlayerCache(pin.ID) : null;
            if (cached?.CharData == null)
                return;

            Image mark = PinGetImage?.Invoke(pin, PinMarkImage);
            if (mark != null)
                mark.sprite = Managers.Resource.Load<Sprite>(cached.CharData.Type + BlackKey);
        }
    }
}
