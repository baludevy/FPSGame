using UnityEngine;

public class ServerSend {
    private static void SendTCPData(int toClient, Packet packet) {
        packet.WriteLength();
        Server.clients[toClient].tcp.SendData(packet);
    }

    private static void SendTCPDataToAll(Packet packet) {
        packet.WriteLength();
        for (int i = 1; i <= Server.MaxPlayers; i++) {
            Server.clients[i].tcp.SendData(packet);
        }
    }

    private static void SendTCPDataToAllExcept(int exceptClient, Packet packet) {
        packet.WriteLength();
        for (int i = 1; i <= Server.MaxPlayers; i++) {
            if (i != exceptClient) {
                Server.clients[i].tcp.SendData(packet);
            }
        }
    }

    private static void SendUDPData(int toClient, Packet packet) {
        packet.WriteLength();
        Server.clients[toClient].udp.SendData(packet);
    }

    private static void SendUDPDataToAll(Packet packet) {
        packet.WriteLength();
        for (int i = 1; i <= Server.MaxPlayers; i++) {
            Server.clients[i].udp.SendData(packet);
        }
    }

    private static void SendUDPDataToAllExcept(int exceptClient, Packet packet) {
        packet.WriteLength();
        for (int i = 1; i <= Server.MaxPlayers; i++) {
            if (i != exceptClient) {
                Server.clients[i].udp.SendData(packet);
            }
        }
    }


    public static void Welcome(int toClient) {
        using (Packet packet = new Packet((int)ServerPackets.welcome)) {
            packet.Write(toClient);

            SendTCPData(toClient, packet);
        }
    }

    public static void MeasureRTT(int toClient, double timestamp)
    {
        using (Packet packet = new Packet((int)ServerPackets.measureRtt))
        {
            packet.Write(timestamp);
            
            SendTCPData(toClient, packet);
        }
    }
    
    public static void SyncTick(int toClient, double timestamp)
    {
        using (Packet packet = new Packet((int)ServerPackets.syncTick))
        {
            packet.Write(timestamp);
            packet.Write(NetworkManager.tick);

            SendTCPData(toClient, packet);
        }
    }

    
    public static void SpawnPlayer(int toClient, Player player) {
        using (Packet packet = new Packet((int)ServerPackets.spawnPlayer)) {
            packet.Write(player.id);
            packet.Write(player.username);
            packet.Write(player.transform.position);
            packet.Write(player.transform.rotation);

            SendTCPData(toClient, packet);
        }
    }

    public static void WorldSnapshot(int toClient, WorldSnapshot snapshot) {
        using (Packet packet = new Packet((int)ServerPackets.worldSnapshot)) {
            packet.Write(snapshot.serverTick);
            packet.Write(snapshot.bufferSlack);
            packet.Write(snapshot.echoTimestamp);

            packet.Write(snapshot.playerStates.Count);

            foreach (PlayerState state in snapshot.playerStates) {
                packet.Write(state.id);
                packet.Write(state.position);
                packet.Write(state.velocity);
                packet.Write(state.orientation);
            }

            SendUDPData(toClient, packet);
        }
    }
}