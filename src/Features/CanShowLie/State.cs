// 作用：维护伪证的持久状态；不负责补丁挂载与界面渲染。
using System.Collections.Generic;
using Protocol;

namespace DT_Tools.Features.CanShowLie
{
    internal static class CanShowLieState
    {
        public static List<AreaInitInfo> AreaCache;
    }
}
