namespace DT_Tools.Core
{
    /// <summary>
    /// 功能作用面：影响谁、在什么角色下生效。
    /// </summary>
    public enum FeatureSide
    {
        /// <summary>仅本地客户端逻辑（移速、UI、解锁展示等）。</summary>
        Client,

        /// <summary>仅房主/主机逻辑（房间规则、强制阶段、多数控制台命令依赖）。</summary>
        Host,

        /// <summary>客户端与主机均可能涉及。</summary>
        Both
    }
}
