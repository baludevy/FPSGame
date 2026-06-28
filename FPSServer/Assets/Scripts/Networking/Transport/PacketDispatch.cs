using System;
using System.Collections.Generic;
using UnityEngine;

public static class PacketDispatch {
    public static void Route(int fromClient, byte[] packetBytes,
        HashSet<byte> offMainThread, Dictionary<byte, Server.PacketHandler> handlers) {
        if (packetBytes == null || packetBytes.Length < 1) return;
        byte packetId = packetBytes[0];

        if (offMainThread.Contains(packetId)) {
            Invoke(fromClient, packetBytes, handlers);
        }
        else {
            byte[] captured = packetBytes;
            ThreadManager.ExecuteOnMainThread(() => Invoke(fromClient, captured, handlers));
        }
    }

    private static void Invoke(int fromClient, byte[] bytes,
        Dictionary<byte, Server.PacketHandler> handlers) {
        using Packet packet = new Packet(bytes);
        byte id = packet.ReadByte();
        if (!handlers.TryGetValue(id, out var handler)) return;
        try {
            handler(fromClient, packet);
        }
        catch (Exception ex) {
            Debug.LogException(ex);
        }
    }
}