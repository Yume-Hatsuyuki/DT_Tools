using System.Collections.Generic;
using UnityEngine.UI;

namespace DT_Tools.Features.CharacterMapPin
{
    internal static class CharacterMapPinState
    {
        public const float CharacterArrowOrbit = 300f;

        public static readonly Dictionary<int, ArrowState> Arrows = new Dictionary<int, ArrowState>();

        public static readonly HashSet<int> KeptPinIds = new HashSet<int>();
    }

    internal sealed class ArrowState
    {
        public UI_Arrow Arrow;
        public Image Mark;
        public string SpriteKey;
    }
}
