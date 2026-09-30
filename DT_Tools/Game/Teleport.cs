using System;
using Protocol;
using UnityEngine;

namespace DT_Tools.Game
{
    /// <summary>本机瞬移：与 /beacon、官方 HandleRespawn 同款（本地落位 + C_MOVE + 相机跟随）。</summary>
    public static class Teleport
    {
        public static bool TryTeleport(MyPlayer my, PosInfo pos, out string error)
        {
            error = null;
            if (my == null)
            {
                error = "MyPlayer is null";
                return false;
            }
            if (pos == null)
            {
                error = "pos is null";
                return false;
            }
            if (my.State == EPlayerState.Hide || my.State == EPlayerState.Sit)
            {
                error = $"state={my.State} blocked";
                return false;
            }

            try
            {
                my.TargetPos = pos;
                my.transform.position = new Vector3(pos.X, pos.Y, 0f);

                if (!ClientPackets.TrySend(new C_MOVE
                {
                    Pos = pos,
                    LookLeft = my.PublicInfo.LookLeft,
                    Velocity = 0f,
                    IsMove = false
                }, out string sendError))
                {
                    error = sendError;
                    return false;
                }

                try
                {
                    var cam = Camera.main;
                    if (cam != null)
                        cam.GetComponent<FollowCamera>()?.Move();
                }
                catch
                {
                    // 相机失败不影响瞬移
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}
