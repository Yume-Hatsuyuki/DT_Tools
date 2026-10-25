using Data;
using Protocol;

namespace DT_Tools.Game
{
    public static class RoomLabel
    {
        public const float GridScale = 224f;

        private const string UnknownLocalized = "未知";
        private const string UnknownRaw = "Unknown";

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
