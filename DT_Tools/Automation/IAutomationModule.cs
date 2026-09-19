using DT_Tools.Core;

namespace DT_Tools.Automation
{
    /// <summary>
    /// 自动化模块契约：配置热读、主线程 Tick、独立日志。
    /// </summary>
    internal interface IAutomationModule
    {
        string Id { get; }
        string Section { get; }
        string DisplayName { get; }
        string Description { get; }
        FeatureSide Side { get; }
        string Author { get; }

        bool ModuleEnabled { get; }

        AutomationLogBuffer Log { get; }

        /// <summary>绑定本模块配置段（不含总开关）。</summary>
        void BindConfig(BepInEx.Configuration.ConfigFile config);

        /// <summary>主线程周期；hostEnabled 为 [Automation].Enabled。</summary>
        void Tick(bool hostEnabled);
    }
}
