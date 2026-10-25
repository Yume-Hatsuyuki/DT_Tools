using System;
using System.Collections.Generic;
using Protocol;
using UnityEngine;

namespace DT_Tools.Features.StrokeTools
{
    internal static class StrokeRouteLogic
    {
        public static Vector2 GetMapOrigin(Data.MapData map)
        {
            return new Vector2(5488f, 4737.5f);
        }

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
                return false;
            type = (ERoomType)value;
            return true;
        }

        private static bool IsCorridorLike(ERoomType type)
        {
            string n = Enum.GetName(typeof(ERoomType), type);
            if (n == null)
                return false;
            return n.Contains("Corridor") || n.EndsWith("Edge") || n == "Tunnel";
        }
    }
}
