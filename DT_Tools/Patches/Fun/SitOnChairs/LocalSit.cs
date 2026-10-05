using System;
using DT_Tools.Core;
using Protocol;
using Spine.Unity;
using UnityEngine;

namespace DT_Tools.Patches.Fun.SitOnChairs
{
    /// <summary>
    /// 等待室（大厅）本地坐姿状态机（原 IslandMap LocalSit）：服务端在 Lobby 阶段
    /// 不创建任何设备对象（0.1.16b Server.Game/GameRoom.cs:678 InitDevices() 只在
    /// GameStart 时调用），所以等待室咖啡椅只能本地假装坐下：钉住玩家位置、播放
    /// 14_Sitting 坐姿动画、把玩家渲染层级抬到 260，起身还原位置与动画；不发送
    /// 任何网络包，别人看不到（引擎限制）。仅当 CafeSit=true 且处于 Lobby 状态时启用。
    /// </summary>
    internal static class LocalSit
    {
        private static Chair _chair;

        private static Vector3 _savedPos;

        private static int _savedOrder = int.MinValue;

        internal static bool IsSitting => _chair != null;

        internal static bool Enabled =>
            SitOnChairsFeature.CafeSit;

        internal static bool IsOn(Chair c)
        {
            if (_chair != null && c != null)
            {
                return _chair == c;
            }
            return false;
        }

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
            if (IsOn(chair))
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
                my.transform.position = SeatPos(chair);
                PlaySit(my, sit: true);
                Log.Info<SitOnChairsFeature>($"[可坐椅子] 等待室本地坐下 @({chair.transform.position.x:F0},{chair.transform.position.y:F0})");
            }
            catch (Exception ex)
            {
                Log.Error<SitOnChairsFeature>("[可坐椅子] 本地坐下失败：" + ex.Message);
                _chair = null;
            }
        }

        internal static void Stand(MyPlayer my)
        {
            if (_chair == null)
            {
                return;
            }
            _chair = null;
            try
            {
                if (my != null)
                {
                    my.transform.position = _savedPos;
                    PlaySit(my, sit: false);
                }
            }
            catch (Exception ex)
            {
                Log.Error<SitOnChairsFeature>("[可坐椅子] 起身失败：" + ex.Message);
            }
        }

        /// <summary>坐着期间每帧调用：钉住玩家在椅子上；离开 Lobby 立即起身。</summary>
        internal static void Hold()
        {
            if (_chair == null)
            {
                return;
            }
            MyPlayer my = MyPlayer();
            if (my == null)
            {
                _chair = null;
            }
            else if (Managers.Game == null || Managers.Game.State != EGameState.Lobby)
            {
                Stand(my);
            }
            else
            {
                my.transform.position = SeatPos(_chair);
            }
        }

        private static MyPlayer MyPlayer()
        {
            return Managers.Player != null ? Managers.Player.MyPlayer : null;
        }

        private static Vector3 SeatPos(Chair chair)
        {
            Vector3 position = chair.transform.position;
            return new Vector3(position.x, position.y, 0f);
        }

        private static void SetSortingOrder(SkeletonAnimation anim, bool sitting)
        {
            try
            {
                var meshRenderer = anim.GetComponent<MeshRenderer>();
                if (meshRenderer == null)
                {
                    return;
                }
                if (sitting)
                {
                    if (_savedOrder == int.MinValue)
                    {
                        _savedOrder = meshRenderer.sortingOrder;
                    }
                    meshRenderer.sortingLayerName = "Default";
                    meshRenderer.sortingOrder = ChairFactory.CharacterSortingOrder;
                }
                else if (_savedOrder != int.MinValue)
                {
                    meshRenderer.sortingOrder = _savedOrder;
                    _savedOrder = int.MinValue;
                }
            }
            catch (Exception ex)
            {
                Log.Error<SitOnChairsFeature>("[可坐椅子] 本地坐姿层级失败：" + ex.Message);
            }
        }

        private static void PlaySit(MyPlayer my, bool sit)
        {
            SkeletonAnimation skeletonAnim = my.SkeletonAnim;
            if (skeletonAnim == null)
            {
                return;
            }
            try
            {
                SetSortingOrder(skeletonAnim, sit);
                if (skeletonAnim.Skeleton != null)
                {
                    skeletonAnim.Skeleton.SetColor(Color.white);
                }
                if (skeletonAnim.AnimationState != null)
                {
                    skeletonAnim.AnimationState.SetAnimation(0, sit ? "14_Sitting" : "1_Idle", !sit);
                    skeletonAnim.AnimationState.SetAnimation(1, "exp_normal", true);
                }
            }
            catch (Exception ex)
            {
                Log.Error<SitOnChairsFeature>("[可坐椅子] 播放坐姿失败：" + ex.Message);
            }
        }
    }
}
