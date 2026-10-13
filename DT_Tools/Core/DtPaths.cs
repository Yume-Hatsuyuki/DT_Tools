using System.IO;

namespace DT_Tools.Core
{
    /// <summary>
    /// 用户数据根目录 BepInEx/config/DT_Tools/。
    /// </summary>
    /// <remarks>
    /// 插件目录只放 DLL 与 WebUI，更新时整体替换不会丢数据。
    /// 用户数据路径只能从这里取，禁止在别处自行拼接。
    /// </remarks>
    public static class DtPaths
    {
        public static string Root => Path.Combine(BepInEx.Paths.ConfigPath, "DT_Tools");

        /// <summary>
        /// 插件配置（BepInEx ConfigFile）。
        /// </summary>
        public static string Cfg => Under("DT_Tools.cfg");

        /// <summary>
        /// 随身 MP3 歌单。
        /// </summary>
        public static string Mp3Playlist => Under("mp3", "playlist.json");

        /// <summary>
        /// 默认照片目录（PhotoSend / LiveDraw 共用）。
        /// </summary>
        public static string DefaultPhotoFolder => Under("photos");

        public static string Under(params string[] parts) => Path.Combine(Root, Path.Combine(parts));

        /// <summary>
        /// 功能配置了自定义目录则用它，否则用默认照片目录。
        /// </summary>
        /// <param name="configured">功能配置的目录；为空或空白时使用默认照片目录。</param>
        /// <returns>最终使用的照片目录路径。</returns>
        public static string ResolvePhotoFolder(string configured) =>
            string.IsNullOrWhiteSpace(configured) ? DefaultPhotoFolder : configured.Trim();

        public static void EnsureRoot() => Directory.CreateDirectory(Root);

        /// <summary>
        /// 写文件前调用：保证父目录存在。
        /// </summary>
        /// <param name="filePath">目标文件的完整路径，函数只创建其父目录。</param>
        public static void EnsureParentOf(string filePath) =>
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
    }
}
