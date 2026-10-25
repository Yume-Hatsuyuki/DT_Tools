using System.IO;

namespace DT_Tools.Core
{
    public static class DtPaths
    {
        public static string Root => Path.Combine(BepInEx.Paths.ConfigPath, "DT_Tools");

        public static string Cfg => Under("DT_Tools.cfg");

        public static string Mp3Playlist => Under("mp3", "playlist.json");
        public static string GamepadMapping => Under("gamepad", "mapping.json");

        public static string DefaultPhotoFolder => Under("photos");

        public static string Under(params string[] parts) => Path.Combine(Root, Path.Combine(parts));

        public static string ResolvePhotoFolder(string configured) =>
            string.IsNullOrWhiteSpace(configured) ? DefaultPhotoFolder : configured.Trim();

        public static void EnsureRoot() => Directory.CreateDirectory(Root);

        public static void EnsureParentOf(string filePath) =>
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
    }
}
