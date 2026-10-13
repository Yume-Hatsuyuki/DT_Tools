using System;
using System.Collections.Generic;
using Protocol;
using UnityEngine;

namespace DT_Tools.Features.StrokeTools
{
    /// <summary>
    /// 路线解读辅助逻辑（独立于补丁类，避免 Harmony 补丁参数静态分析误报）。
    /// </summary>
    /// <remarks>
    /// 房间识别与官方一致：格子 = 世界坐标 ÷ 224 − MapOffset（0.1.16b
    /// Server.Game/AreaManager.cs:79 GetArea；ValidPosition 同文件 :89-99 边界检查），
    /// 格子值 = Managers.Data.MapArray[x][y]（DataManager.cs:28，byte 即 ERoomType），
    /// 0 为 DefaultRoom（空地）。房间名 = Managers.GetText(枚举名)（0.1.17a UI_ChatDevicePopup.cs:471）。
    /// </remarks>
    internal static class StrokeRouteLogic
    {
        /// <summary>
        /// 平板坐标 → 世界坐标所需的原点平移。
        /// </summary>
        /// <remarks>
        /// 画布（对象88/89）覆盖地图显示区域（对象10），对象10 sizeDelta = 世界尺寸 × 0.094（0.1.17a UI_GameTablet.cs:1189 InitConstMap(9184,7235)），UI 局部坐标原点 = RectTransform 中心（ScreenPointToLocalPointInRectangle 相对 pivot，默认 0.5）。
        /// 世界地图范围由 MapOffset 定义：X ∈ [896, 896+9184]（4 格×224）、Y ∈ [1120, 1120+7235]（5 格×224，0.1.17a MapManager.cs:274 IsValidPos x=(x−896)/224, y=(y−1120)/224）。中心 = ((896+10080)/2, (1120+8355)/2) = (5488, 4737.5)。world = 局部 ÷ scale + origin（scale = MinimapScale，正式图 0.094，与对象10 缩放一致）。
        /// </remarks>
        /// <param name="map">当前地图数据。</param>
        /// <returns>平板坐标换算到世界坐标所需的原点平移量。</returns>
        public static Vector2 GetMapOrigin(Data.MapData map)
        {
            return new Vector2(5488f, 4737.5f);
        }

        /// <summary>
        /// 采样点归属房间：与 route 末尾相同则忽略（连续同房合并），不同则追加。
        /// </summary>
        /// <remarks>
        /// 空地/越界（TryGetRoom 返回 false）与配置过滤的走廊直接跳过该点。
        /// </remarks>
        /// <param name="route">已记录的房间路线。</param>
        /// <param name="world">采样点的世界坐标。</param>
        /// <param name="map">当前地图数据。</param>
        /// <param name="cell">格子边长，单位世界单位。</param>
        public static void AppendRoomAt(List<string> route, Vector2 world, Data.MapData map, float cell)
        {
            if (!TryGetRoom(world, map, cell, out ERoomType type))
                return;
            if (!StrokeToolsFeature.IncludeCorridors && IsCorridorLike(type))
                return;
            string name = Managers.GetText(Enum.GetName(typeof(ERoomType), type));
            if (string.IsNullOrEmpty(name))
                return;
            if (route.Count > 0 && route[route.Count - 1] == name)
                return;
            route.Add(name);
        }

        /// <summary>
        /// 世界坐标 → 格子 → 房间类型。
        /// </summary>
        /// <remarks>
        /// 索引与官方一致：MapArray[x][y]（x = pos.X/格 − Offset.X，
        /// y = pos.Y/格 − Offset.Y，0.1.16b AreaManager.cs:79（待核对）)；边界与空地检查照抄
        /// AreaManager.ValidPosition（:89-99）。
        /// </remarks>
        /// <param name="world">世界坐标。</param>
        /// <param name="map">当前地图数据。</param>
        /// <param name="cell">格子边长，单位世界单位。</param>
        /// <param name="type">采样点所在的房间类型；返回 false 时无效。</param>
        /// <returns>坐标落在地图有效范围内时为 true。</returns>
        private static bool TryGetRoom(Vector2 world, Data.MapData map, float cell, out ERoomType type)
        {
            type = ERoomType.DefaultRoom;
            float wx = world.x;
            float wy = world.y;
            int x = (int)(wx / cell) - (int)map.MapOffset.X;
            int y = (int)(wy / cell) - (int)map.MapOffset.Y;
            if (x < 0 || y < 0 || (float)x >= map.MapSize.X || (float)y >= map.MapSize.Y)
                return false;
            if (x >= Managers.Data.MapArray.Count)
                return false;
            List<byte> row = Managers.Data.MapArray[x];
            if (row == null || y >= row.Count)
                return false;
            byte value = row[y];
            if (value == 0)
                return false; // DefaultRoom=0：空地
            type = (ERoomType)value;
            return true;
        }

        /// <summary>
        /// 走廊/边缘通道：枚举名含 Corridor 或 Edge，以及 Tunnel（隧道）。
        /// </summary>
        /// <param name="type">房间类型。</param>
        /// <returns>是走廊或边缘通道时为 true。</returns>
        private static bool IsCorridorLike(ERoomType type)
        {
            string n = Enum.GetName(typeof(ERoomType), type);
            if (n == null)
                return false;
            return n.Contains("Corridor") || n.EndsWith("Edge") || n == "Tunnel";
        }
    }
}
