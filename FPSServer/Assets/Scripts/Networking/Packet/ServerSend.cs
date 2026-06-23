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

    public static void SyncTick(int toClient, float clientSendTime, uint serverTick) {
        using (Packet packet = new Packet((int)ServerPackets.syncTick)) {
            packet.Write(clientSendTime);
            packet.Write(serverTick);

            SendUDPData(toClient, packet);
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

    public static void GameUpdate(int toClient, GameUpdate update) {
        using (Packet packet = new Packet((int)ServerPackets.gameUpdate)) {
            packet.Write(update.serverTick);
            packet.Write(update.serverReceiveMargin);
            packet.Write(update.serverInputJitter);

            packet.Write(update.clientSendTime);
            packet.Write(update.serverSendTime);
            packet.Write(update.serverReceiveTime);

            packet.Write(update.movementState.id);
            packet.Write(update.movementState.position);
            packet.Write(update.movementState.orientation);
            packet.Write(update.movementState.velocity);

            packet.Write((byte)update.worldSnapshot.playerStates.Count);
            foreach (PlayerState state in update.worldSnapshot.playerStates) {
                packet.Write(state.id);
                packet.Write(state.position);
                packet.Write(state.crouching);
            }

            SendUDPData(toClient, packet);
        }
    }

    public static void LagCompVisual(int toClient, Vector3 pos) {
        using (Packet packet = new Packet((int)ServerPackets.lagCompVisual)) {
            packet.Write(pos);

            SendUDPData(toClient, packet);
        }
    }
}