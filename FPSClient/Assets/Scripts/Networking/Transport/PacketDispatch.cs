using System;
using System.Collections.Generic;
using PimDeWitte.UnityMainThreadDispatcher;
using UnityEngine;

public static class PacketDispatch {
    public static void Route(byte[] packetBytes, HashSet<byte> offMainThread,
        Dictionary<byte, Action<Packet>> handlers) {
        if (packetBytes == null || packetBytes.Length < 1) return;
        byte packetId = packetBytes[0];

        if (offMainThread.Contains(packetId)) {
            Invoke(packetBytes, handlers);
        }
        else {
            byte[] captured = packetBytes;
            UnityMainThreadDispatcher.Instance().Enqueue(() => Invoke(captured, handlers));
        }
    }

    private static void Invoke(byte[] bytes, Dictionary<byte, Action<Packet>> handlers) {
        using Packet packet = new Packet(bytes);
        byte id = packet.ReadByte();
        if (!handlers.TryGetValue(id, out Action<Packet> handler)) return;
        try {
            handler(packet);
        }
        catch (Exception ex) {
            Debug.LogException(ex);
            return;
        }

        NetStatistics.packetsReceived++;
        NetStatistics.bytesReceived += packet.Length();
    }
}