using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using Data;
using Protocol;

namespace DT_Tools.Console.Commands.Weapon
{
    /// <summary>
    /// 天匠 / 营图共用：本机客户端身份的对局校验、开放刀架查找、目标解析。
    /// 与 PacketCallHelper（房主端注入）不同，这里走的是本机真实网络链路
    /// <see cref="Managers.Network"/> 的 GameServer.Send，非房主客户端同样可用。
    /// </summary>
    internal static class WeaponPacketHelper
    {
        /// <summary>
        /// 校验本机已进入对局（MyPlayer 已生成、到 Host 的网络链路存在）。
        /// 失败时输出提示并返回 null。
        /// </summary>
        public static MyPlayer RequireLocalPlayer(WebConsole console)
        {
            var my = Managers.Player?.MyPlayer;
            if (my == null || my.PrivateInfo == null)
            {
                console.Log("本机玩家尚未进入对局（大厅/加载中不可用）。", LogLevel.Warning);
                return null;
            }
            if (Managers.Network == null || Managers.Network.GameServer == null)
            {
                console.Log("未连接到 Host（GameServer 链路为空），无法发送 C_ 包。", LogLevel.Warning);
                return null;
            }
            return my;
        }

        /// <summary>
        /// 校验当前为生存阶段。武器架交互与递刀的服务端入口都只在 Survive 生效。
        /// </summary>
        public static bool RequireSurvive(WebConsole console)
        {
            if (Managers.Game == null || Managers.Game.State != EGameState.Survive)
            {
                console.Log($"仅生存阶段可用，当前状态: {(Managers.Game == null ? "(null)" : Managers.Game.State.ToString())}。",
                    LogLevel.Warning);
                return false;
            }
            return true;
        }

        /// <summary>
        /// 查找当前处于 OpenArmory(State==1) 的武器架。
        /// 刀架状态经 S_MODIFY_DEVICE 全员广播，任何客户端都持有实时状态。
        /// </summary>
        public static List<DeviceBase> FindOpenArmories()
        {
            return Managers.Device.Cache.Values
                .Where(d => d != null
                            && d.DeviceType == EDeviceType.Armory
                            && d.DeviceState == (int)EArmoryState.OpenArmory)
                .OrderBy(d => d.ID)
                .ToList();
        }

        /// <summary>
        /// 从本机客户端设备缓存中取出所有武器架（DeviceType==Armory），按 ID 排序。
        /// 武器架在 S_INIT_MAP 时随房间数据生成，全程常驻 Cache。
        /// </summary>
        public static List<DeviceBase> GetAllArmories()
        {
            return Managers.Device.Cache.Values
                .Where(d => d != null && d.DeviceType == EDeviceType.Armory)
                .OrderBy(d => d.ID)
                .ToList();
        }

        /// <summary>
        /// 解析武器架所在地区：DeviceData.RoomType（与服务端 AreaManager.GetArea(pos).Data 同源，
        /// 设备摆放在哪个房间由配置写死）→ TextData 本地化房间名。
        /// 返回 (本地化名称, ERoomType 原名)；无文本时本地化名称回退为枚举名。
        /// </summary>
        public static (string localized, string raw) GetRoomLabel(DeviceBase armory)
        {
            ERoomType roomType = armory?.Data?.RoomType ?? ERoomType.DefaultRoom;
            string key = roomType.ToString();
            var textDic = Managers.Data?.TextDic;
            string localized = (textDic != null && textDic.TryGetValue(key, out var text) && text != null)
                ? text.Text
                : key;
            return (localized, key);
        }

        /// <summary>
        /// 武器架空场后自动换点的剩余秒数（StateList[2]=总时长, [3]=已计时）。
        /// 仅 OpenArmory 有意义；无法取用时返回 null。
        /// </summary>
        public static int? TryGetMoveRemainSeconds(DeviceBase armory)
        {
            var states = armory?.Info?.StateList;
            if (states == null || states.Count <= 3) return null;
            int remain = states[2] - states[3];
            return remain < 0 ? 0 : remain;
        }

        /// <summary>
        /// 解析 #123 或纯数字 123。
        /// </summary>
        public static bool TryParseId(string token, out int id, out string error)
        {
            id = 0;
            error = null;
            if (token.StartsWith("#") && int.TryParse(token.Substring(1), out id))
                return true;
            if (int.TryParse(token, out id))
                return true;
            error = $"无效的 ID: {token}（应为 #<数字> 或纯数字）";
            return false;
        }

        /// <summary>
        /// 从本机客户端玩家表查找目标。
        /// </summary>
        public static Player FindClientPlayer(int playerId)
        {
            var dict = Managers.Player?.Players;
            if (dict == null) return null;
            return dict.TryGetValue(playerId, out var p) ? p : null;
        }
    }
}
