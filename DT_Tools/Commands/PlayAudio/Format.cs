namespace DT_Tools.Commands.PlayAudio
{
    internal static class PlayAudioFormat
    {
        public static string Reply(PlayAudioArgs args)
        {
            string durationText = args.MaxSeconds > 0f ? $"{args.MaxSeconds:0.##} 秒" : "完整播放";
            return $"已点播：{args.Source}\n"
                 + $"本地即播（时长={durationText}，响度={args.Volume:0.##}，与「StageMusic」的阶段配置无关）；"
                 + "点播期间独占播放槽，结束后自动恢复阶段音乐；"
                 + "麦克风广播需启用「StageMusic」并开启 MicBroadcast、麦克风未静音。";
        }
    }
}
