using DT_Tools.Game;

namespace DT_Tools.Commands.MicMusic
{
    internal static class MicMusicFormat
    {
        private static string KindText(AudioPlaybackSource kind)
            => kind switch
            {
                AudioPlaybackSource.Local => "本地文件",
                AudioPlaybackSource.Online => "在线链接",
                _ => "自动识别（http(s)=在线，其余本地）",
            };

        public static string Reply(MicMusicArgs args)
        {
            if (args.Off)
                return "虚拟麦克风已停止，采集管线与语音处理设置还原。";
            string durationText = args.MaxSeconds > 0f ? $"{args.MaxSeconds:0.##} 秒" : "完整播放";
            return $"虚拟麦克风广播中：{args.Source}\n"
                 + $"来源={KindText(args.SourceKind)}，时长={durationText}，响度={args.Volume:0.##}；"
                 + "音乐已混入你的语音发往全房（无需说话，游戏内麦克风不能静音）；\n"
                 + "广播期间游戏语音降噪/回声消除已临时关停（结束自动还原），否则音乐会播碎；"
                 + "残余「变声器感」为 Opus 32kbps 语音编码天花板；\n"
                 + "播完自动卸载，/mic_music off 或 /stop_music 可提前停止。";
        }
    }
}
