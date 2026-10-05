using System;
using Newtonsoft.Json;

namespace DT_Tools.Commands
{
    /// <summary>
    /// 命令结果信封：Newtonsoft 序列化为 {"ok":…,"error":…,"data":…}（camelCase）。
    /// data 用匿名对象 / DTO（键名即想要的 JSON 形状），禁止字符串拼接。
    /// 错误码约定：小写英文短词（"host only"、"no room"、"invalid target"），与前端约定一致。
    /// </summary>
    public sealed class CommandResult
    {
        public bool Ok { get; private set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Error { get; private set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public object Data { get; private set; }

        /// <summary>
        /// 延迟完成回调（ctx.Defer() 通道挂入）：非空表示本结果是占位、真实结果稍后经
        /// <see cref="Complete"/> 回填。等待方（pump/RunOnMain）据此跳过立即回填。
        /// </summary>
        private Action<CommandResult> _deferredComplete;

        [JsonIgnore]
        public bool IsDeferred => _deferredComplete != null;

        private CommandResult(bool ok, string error, object data)
        {
            Ok = ok;
            Error = error;
            Data = data;
        }

        public static CommandResult Success(object data = null) => new CommandResult(true, null, data);

        public static CommandResult Fail(string error, object data = null)
            => new CommandResult(false, string.IsNullOrEmpty(error) ? "error" : error, data);

        /// <summary>构造延迟占位结果：Execute 直接 return 它，真实结果稍后 Complete(final) 回填。</summary>
        public static CommandResult Defer(Action<CommandResult> complete)
            => new CommandResult(true, null, null) { _deferredComplete = complete ?? throw new ArgumentNullException(nameof(complete)) };

        /// <summary>回填真实结果（只认第一次；主线程调用）。</summary>
        public void Complete(CommandResult final)
        {
            var sink = _deferredComplete;
            _deferredComplete = null;
            sink?.Invoke(final ?? Fail("no result"));
        }
    }
}
