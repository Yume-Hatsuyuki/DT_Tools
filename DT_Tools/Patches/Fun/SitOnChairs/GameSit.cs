using System;
using DT_Tools.Core;
using Protocol;
using UnityEngine;

namespace DT_Tools.Patches.Fun.SitOnChairs
{
    /// <summary>
    /// 对局内（非 Lobby）椅子坐姿状态机，完全复刻游戏原生坐姿机制：
    /// 原版坐下的表现 = 玩家模型隐藏（MyPlayer.HidePlayer(true)）+
    /// 椅子 Spine 换成"角色坐在椅子上的形象"（Chair.ChangeCharacter(角色ID)
    /// 加载角色骨架并播 14_Sitting 入座动画）；起身 = 玩家模型显示 +
    /// 椅子形象还原（ChangeCharacter(0)，Spine 转透明）。
    /// 原版 Chair.Interact 只发 C_INTERACT_CHAIR 等服务端（房主）响应——房主未装本
    /// mod / 服务端没有这把椅子时永久无响应，导致"按 E 没反应"；本状态机在按 E 时
    /// 本地立即执行同样的坐姿表现，同时照发原版网络包（房主装了则全房同步，
    /// 没装也不影响本地坐姿）。起身同理本地还原。
    /// </summary>
    internal static class GameSit
    {
        /// <summary>坐姿同步广播前缀（零宽字符 + 左方括号，与表情动作同步的前缀区分开）。</summary>
        internal const string SyncPrefix = "\u200B[";
        internal const string SyncSuffix = "]";

        private static Chair _chair;

        private static Vector3 _savedPos;

        internal static bool IsSitting => _chair != null;

        internal static void Toggle(Chair chair)
        {
            if (chair == null)
            {
                return;
            }
            MyPlayer my = MyPlayer();
            if (my == null)
            {
                return;
            }
            if (_chair == chair)
            {
                Stand(my);
                return;
            }
            if (_chair != null)
            {
                Stand(my);
            }
            _chair = chair;
            _savedPos = my.transform.position;
            try
            {
                // 1) 隐藏玩家模型（原版 Sit 状态的表现）
                my.HidePlayer(isHide: true);
                // 2) 椅子 Spine 换成"角色坐姿形象"并播 14_Sitting 入座动画
                int charId = FindMyCharacterId(my);
                chair.ChangeCharacter(charId, isInit: false);
                Log.Info<SitOnChairsFeature>($"[可坐椅子] 对局内坐下 @({chair.transform.position.x:F0},{chair.transform.position.y:F0}) charId={charId}");
                // 3) 按配置广播坐姿（装本 mod 的玩家互见，无需房主）
                BroadcastSit(chair, charId);
            }
            catch (Exception ex)
            {
                Log.Error<SitOnChairsFeature>("[可坐椅子] 对局内坐下失败：" + ex.Message);
                TryRestore(my, chair);
                _chair = null;
            }
        }

        internal static void Stand(MyPlayer my)
        {
            if (_chair == null)
            {
                return;
            }
            Chair chair = _chair;
            _chair = null;
            try
            {
                // 0) 按配置广播起身（装本 mod 的玩家互见还原）
                BroadcastRise(chair);
                // 1) 椅子形象还原（Spine 转透明）
                chair.ChangeCharacter(0, isInit: true);
                // 2) 玩家模型重新显示，回到坐下前位置
                if (my != null)
                {
                    my.HidePlayer(isHide: false);
                    my.transform.position = _savedPos;
                }
            }
            catch (Exception ex)
            {
                Log.Error<SitOnChairsFeature>("[可坐椅子] 对局内起身失败：" + ex.Message);
            }
        }

        /// <summary>坐着期间每帧调用：离开调查/生存阶段或死亡时立即起身还原。</summary>
        internal static void Hold()
        {
            if (_chair == null)
            {
                return;
            }
            MyPlayer my = MyPlayer();
            if (my == null)
            {
                if (_chair != null)
                {
                    _chair.ChangeCharacter(0, isInit: true);
                    _chair = null;
                }
            }
            else if (Managers.Game == null || !Managers.Game.IsAlive
                || (Managers.Game.State != EGameState.Survive && Managers.Game.State != EGameState.Detective))
            {
                Stand(my);
            }
        }

        private static MyPlayer MyPlayer()
        {
            return Managers.Player != null ? Managers.Player.MyPlayer : null;
        }

        /// <summary>自己角色的 ID（PublicInfo.CharacterId，椅子的 ChangeCharacter 需要这个 ID）。</summary>
        private static int FindMyCharacterId(MyPlayer my)
        {
            try
            {
                if (my?.PublicInfo != null)
                {
                    return my.PublicInfo.CharacterId;
                }
            }
            catch (Exception ex)
            {
                Log.Error<SitOnChairsFeature>("[可坐椅子] 查找角色ID失败：" + ex.Message);
            }
            return 0;
        }

        /// <summary>广播坐下：零宽前缀指令，房主当作普通聊天原样转发全房，装本 mod 的客户端解析。</summary>
        private static void BroadcastSit(Chair chair, int charId)
        {
            if (!SitOnChairsFeature.SyncSitting)
            {
                return;
            }
            try
            {
                Managers.Voice?.SendChatMessage(SyncPrefix + "SIT|" + chair.Info.DeviceId + "|" + charId + SyncSuffix);
            }
            catch (Exception ex)
            {
                Log.Warn<SitOnChairsFeature>("[可坐椅子] 坐姿同步广播(坐下)失败：" + ex.Message);
            }
        }

        /// <summary>广播起身：装本 mod 的客户端据此还原椅子形象并显示该玩家模型。</summary>
        private static void BroadcastRise(Chair chair)
        {
            if (!SitOnChairsFeature.SyncSitting)
            {
                return;
            }
            try
            {
                Managers.Voice?.SendChatMessage(SyncPrefix + "RISE|" + chair.Info.DeviceId + SyncSuffix);
            }
            catch (Exception ex)
            {
                Log.Warn<SitOnChairsFeature>("[可坐椅子] 坐姿同步广播(起身)失败：" + ex.Message);
            }
        }

        private static void TryRestore(MyPlayer my, Chair chair)
        {
            try
            {
                if (chair != null)
                {
                    chair.ChangeCharacter(0, isInit: true);
                }
                if (my != null)
                {
                    my.HidePlayer(isHide: false);
                    my.transform.position = _savedPos;
                }
            }
            catch
            {
            }
        }
    }
}
