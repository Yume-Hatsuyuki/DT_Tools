using System.Collections.Generic;
using UnityEngine.UI;

namespace DT_Tools.Patches.Experience.CharacterMapPin
{
    /// <summary>运行期状态：常驻角色箭头与轨道参数。只存字段，游戏调用在 Ui / Logic。</summary>
    internal static class CharacterMapPinState
    {
        /// <summary>
        /// 常驻箭头轨道半径：与 Kaho 角色箭头一致（原版 UI_Arrow.SetInfo(ECharacterType,...)
        /// 写 _orbitRadius = 300，0.1.15b UI_Arrow.cs:77；字段默认 200，0.1.15b UI_Arrow.cs:14）。
        /// </summary>
        public const float CharacterArrowOrbit = 300f;

        /// <summary> playerId → 常驻箭头运行态。</summary>
        public static readonly Dictionary<int, ArrowState> Arrows = new Dictionary<int, ArrowState>();

        /// <summary>
        /// 「死亡保留 Pin」拦下未删的玩家 id：TabletManager.DeletePin 前置拦截时记录
        ///（0.1.15b TabletManager.cs:133）。玩家本体已 Despawn（0.1.15b PlayerManager.cs:424），
        /// 白方巡检与热关闭清理都按此集合识别保留 pin。集合残留跨回合无害：
        /// 新对局同 id 玩家的删除拦截行为与其身份无关，语义不变。
        /// </summary>
        public static readonly HashSet<int> KeptPinIds = new HashSet<int>();

        public static void Reset()
        {
            Arrows.Clear();
            KeptPinIds.Clear();
        }
    }

    /// <summary>单支常驻箭头的运行态：箭头视图、Mark 贴图槽、当前已套用的贴图键。</summary>
    internal sealed class ArrowState
    {
        public UI_Arrow Arrow;
        public Image Mark;
        public string SpriteKey;
    }
}
