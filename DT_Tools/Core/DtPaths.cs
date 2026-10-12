using System.IO;

namespace DT_Tools.Core
{
    /// 用户数据根目录 BepInEx/config/DT_Tools/。插件目录只放 DLL 与 WebUI，更新时整体替换不会丢数据。
    /// 用户数据路径只能从这里取，禁止在别处自行拼接。
    public static class DtPaths
    {
        public static string Root => Path.Combine(BepInEx.Paths.ConfigPath, "DT_Tools");

        /// 插件配置（BepInEx ConfigFile）。
        public static string Cfg => Under("DT_Tools.cfg");

        /// 随身 MP3 歌单。
        public static string Mp3Playlist => Under("mp3", "playlist.json");

        /// 默认照片目录（PhotoSend / LiveDraw 共用）。
        public static string DefaultPhotoFolder => Under("photos");

        public static string Under(params string[] parts) => Path.Combine(Root, Path.Combine(parts));

        /// 功能配置了自定义目录则用它，否则用默认照片目录。
        public static string ResolvePhotoFolder(string configured) =>
            string.IsNullOrWhiteSpace(configured) ? DefaultPhotoFolder : configured.Trim();

        public static void EnsureRoot() => Directory.CreateDirectory(Root);

        /// 写文件前调用：保证父目录存在。
        public static void EnsureParentOf(string filePath) =>
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
    }
}
