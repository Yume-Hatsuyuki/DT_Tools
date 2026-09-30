using Data;
using Protocol;

namespace DT_Tools.Game
{
    /// <summary>
    /// 房间名本地化助手（收敛旧 WeaponPacketHelper.GetRoomLabel 与 BeaconCommand.ResolveRoomLabel，
    /// 两处共享同一条 TextDic 本地化路径，此处合并为唯一实现）。
    ///   - FromDevice：设备摆放地区取 DeviceData.RoomType（配置写死，与服务端
    ///     AreaManager.GetArea(pos).Data 同源）→ TextDic 本地化名（与平板扫描 UI_GameTablet.SetWeaponInfo 同源）。
    ///   - FromPos：坐标按 224 网格反查 MapArray → ERoomType → TextDic，与服务端 GetArea 同款算法。
    ///     见 0.1.15b Server.Game/AreaManager.cs:74-80（GetArea）、DataManager.cs:28(MapArray)/30(TextDic)、
    ///     Data/DeviceData.cs:34(RoomType)、Data/TextData.cs:10(Text)。
    /// 返回 (本地化名称, raw 枚举名)；无文本时本地化名称回退为枚举名。
    /// </summary>
    public static class RoomLabel
    {
        /// <summary>单格边长（AreaManager.GetArea / ValidPosition / ClampToMapBounds 的网格尺度）。</summary>
        public const float GridScale = 224f;

        private const string UnknownLocalized = "未知";
        private const string UnknownRaw = "Unknown";

        /// <summary>坐标 → 房间名（224 网格反查 MapArray）。/beacon 列表与落点显示用。</summary>
        public static (string localized, string raw) FromPos(PosInfo pos)
        {
            var mapData = Managers.Data?.MapData;
            if (pos == null || mapData?.MapOffset == null)
                return (UnknownLocalized, UnknownRaw);

            var mapArray = Managers.Data?.MapArray;
            if (mapArray == null || mapArray.Count == 0 || mapArray[0] == null || mapArray[0].Count == 0)
                return (UnknownLocalized, UnknownRaw);

            int gx = (int)(pos.X / GridScale) - (int)mapData.MapOffset.X;
            int gy = (int)(pos.Y / GridScale) - (int)mapData.MapOffset.Y;
            if (gx < 0 || gy < 0 || gx >= mapArray.Count || gy >= mapArray[0].Count)
                return ("地图外", "OutOfBounds");

            byte b = mapArray[gx][gy];
            if (b == 0)
                return ("不可通行", "Blocked");

            return Localize(((ERoomType)b).ToString());
        }

        /// <summary>设备 → 房间名（DeviceData.RoomType）。武器架 / 电闸列表用。</summary>
        public static (string localized, string raw) FromDevice(DeviceBase device)
        {
            ERoomType roomType = device?.Data?.RoomType ?? ERoomType.DefaultRoom;
            return Localize(roomType.ToString());
        }

        private static (string localized, string raw) Localize(string key)
        {
            var textDic = Managers.Data?.TextDic;
            string localized = textDic != null && textDic.TryGetValue(key, out var text) && text != null
                ? text.Text
                : key;
            return (localized, key);
        }
    }
}
