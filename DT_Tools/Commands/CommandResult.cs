using System;
using Newtonsoft.Json;

namespace DT_Tools.Commands
{
    public sealed class CommandResult
    {
        public bool Ok { get; private set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Error { get; private set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public object Data { get; private set; }

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

        public static CommandResult Defer(Action<CommandResult> complete)
            => new CommandResult(true, null, null) { _deferredComplete = complete ?? throw new ArgumentNullException(nameof(complete)) };

        public void Complete(CommandResult final)
        {
            var sink = _deferredComplete;
            _deferredComplete = null;
            sink?.Invoke(final ?? Fail("no result"));
        }
    }
}
