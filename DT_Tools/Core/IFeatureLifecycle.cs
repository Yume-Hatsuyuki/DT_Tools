namespace DT_Tools.Core
{
    /// <summary>
    /// 生命周期方法名约定（静态类无法实现 interface，故用方法名约定）：
    /// OnPatched / OnEnabled / OnDisabled。
    /// 若类型上存在任意一个同名 public/private static 无参方法，PatchLoader 会订阅 Enabled 并调用。
    /// </summary>
    internal static class FeatureLifecycleNames
    {
        public const string OnPatched = "OnPatched";
        public const string OnEnabled = "OnEnabled";
        public const string OnDisabled = "OnDisabled";
    }
}
