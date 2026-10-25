using System;
using DummyClient;
using Google.Protobuf;

namespace DT_Tools.Game
{
    public static class ClientPackets
    {
        public static bool TrySend(IMessage packet, out string error)
        {
            error = null;
            if (packet == null)
            {
                error = "packet is null";
                return false;
            }

            try
            {
                IPacketSink sink = Managers.Network?.GameServer;
                if (sink == null)
                {
                    error = "GameServer session is null";
                    return false;
                }

                sink.Send(packet);
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
