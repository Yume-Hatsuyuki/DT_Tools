using System;
using DT_Tools.Core;
using Protocol;
using UnityEngine;

namespace DT_Tools.Features.SitOnChairs
{
    internal static class GameSit
    {
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
                my.HidePlayer(isHide: true);
                int charId = FindMyCharacterId(my);
                chair.ChangeCharacter(charId, isInit: false);
                Log.Info<SitOnChairsFeature>($"对局内坐下 @({chair.transform.position.x:F0},{chair.transform.position.y:F0}) charId={charId}");
                BroadcastSit(chair, charId);
            }
            catch (Exception ex)
            {
                Log.Error<SitOnChairsFeature>(ex, "对局内坐下失败");
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
                BroadcastRise(chair);
                chair.ChangeCharacter(0, isInit: true);
                if (my != null)
                {
                    my.HidePlayer(isHide: false);
                    my.transform.position = _savedPos;
                }
            }
            catch (Exception ex)
            {
                Log.Error<SitOnChairsFeature>(ex, "对局内起身失败");
            }
        }

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
                Log.Error<SitOnChairsFeature>(ex, "查找角色ID失败");
            }
            return 0;
        }

        private static void BroadcastSit(Chair chair, int charId)
        {
            if (!SitOnChairsFeature.SyncSitting)
            {
                return;
            }
            try
            {
                Managers.Voice?.SendChatMessage(SitSyncWire.Sit(chair.Info.DeviceId, charId));
            }
            catch (Exception ex)
            {
                Log.Warn<SitOnChairsFeature>(ex, "坐姿同步广播(坐下)失败");
            }
        }

        private static void BroadcastRise(Chair chair)
        {
            if (!SitOnChairsFeature.SyncSitting)
            {
                return;
            }
            try
            {
                Managers.Voice?.SendChatMessage(SitSyncWire.Rise(chair.Info.DeviceId));
            }
            catch (Exception ex)
            {
                Log.Warn<SitOnChairsFeature>(ex, "坐姿同步广播(起身)失败");
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
