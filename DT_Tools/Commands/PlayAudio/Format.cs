using DT_Tools.Game;

namespace DT_Tools.Commands.PlayAudio
{
    internal static class PlayAudioFormat
    {
        private static string KindText(AudioPlaybackSource kind)
            => kind switch
            {
                AudioPlaybackSource.Local => "本地文件",
                AudioPlaybackSource.Online => "在线链接",
                _ => "自动识别（http(s)=在线，其余本地）",
            };

        public static string Reply(AudioSourceArgs args)
        {
            string durationText = args.MaxSeconds > 0f ? $"{args.MaxSeconds:0.##} 秒" : "完整播放";
            return $"已点播：{args.Source}\n"
                 + $"来源={KindText(args.SourceKind)}，时长={durationText}，响度={args.Volume:0.##}；"
                 + "点播独立于「StageMusic」的阶段音乐（互不抢占），仅本地播放，/stop_music 停止；"
                 + "要对全房间广播请用 /mic_music。";
        }
    }
}
