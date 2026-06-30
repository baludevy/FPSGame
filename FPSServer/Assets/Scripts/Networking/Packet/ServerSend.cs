using UnityEngine;

public class ServerSend {
    private static void SendTCPData(int toClient, Packet packet) {
        packet.WriteLength();
        Server.clients[toClient].tcp.SendData(packet);
    }

    private static void SendTCPDataToInGame(Packet packet) {
        packet.WriteLength();
        for (int i = 1; i <= Server.maxPlayers; i++) {
            if (Server.clients[i].inGame.IsSet()) {
                Server.clients[i].tcp.SendData(packet);
            }
        }
    }

    private static void SendTCPDataToInGameExcept(int exceptClient, Packet packet) {
        packet.WriteLength();
        for (int i = 1; i <= Server.maxPlayers; i++) {
            if (i != exceptClient && Server.clients[i].inGame.IsSet()) {
                Server.clients[i].tcp.SendData(packet);
            }
        }
    }

    private static void SendUDPData(int toClient, Packet packet) {
        packet.WriteLength();
        Server.clients[toClient].udp.SendData(packet);
    }

    private static void SendUDPDataToInGame(Packet packet) {
        packet.WriteLength();
        for (int i = 1; i <= Server.maxPlayers; i++) {
            if (Server.clients[i].inGame.IsSet()) {
                Server.clients[i].udp.SendData(packet);
            }
        }
    }

    private static void SendUDPDataToInGameExcept(int exceptClient, Packet packet) {
        packet.WriteLength();
        for (int i = 1; i <= Server.maxPlayers; i++) {
            if (i != exceptClient && Server.clients[i].inGame.IsSet()) {
                Server.clients[i].udp.SendData(packet);
            }
        }
    }

    public static void Welcome(int toClient) {
        using (Packet packet = new Packet((int)ServerPackets.welcome)) {
            packet.Write(toClient);

            byte[] token = Server.clients[toClient].IssueNewToken();
            packet.Write(token);

            SendTCPData(toClient, packet);
        }
    }

    public static void UdpConfirmed(int toClient) {
        using (Packet packet = new Packet((int)ServerPackets.udpConfirmed)) {
            packet.Write(toClient);
            SendTCPData(toClient, packet);
        }
    }

    public static void SpawnPlayer(int toClient, Player player) {
        if (!Server.clients[toClient].inGame.IsSet()) {
            return;
        }

        using (Packet packet = new Packet((int)ServerPackets.spawnPlayer)) {
            packet.Write(player.id);
            packet.Write(player.username);
            packet.Write(player.transform.position);
            packet.Write(player.transform.rotation);

            SendTCPData(toClient, packet);
        }
    }

    public static void GameUpdate(int toClient, GameUpdate update) {
        if (!Server.clients[toClient].inGame.IsSet()) {
            return;
        }

        using (Packet packet = new Packet((int)ServerPackets.gameUpdate)) {
            packet.Write(update.serverTick);

            packet.Write(FloatCompressor.FloatToShort(update.timingInfo.inputReceiveMargin));
            packet.Write(update.timingInfo.clientSendTimeAck);
            packet.Write(update.timingInfo.serverSendTime);
            packet.Write(update.timingInfo.serverReceiveTime);

            packet.Write(FloatCompressor.FloatToShort(update.upstreamStatistics.jitter));
            packet.Write(FloatCompressor.FloatToShort(update.upstreamStatistics.packetLoss));

            packet.Write(update.movementState.position);
            packet.Write(update.movementState.velocity);
            packet.Write(update.movementState.orientation);

            packet.Write((byte)update.worldSnapshot.playerStatesCount);
            for (int i = 0; i < update.worldSnapshot.playerStatesCount; i++) {
                PlayerState state = update.worldSnapshot.playerStates[i];
                packet.Write((byte)state.id);
                packet.Write(state.position);
            }

            SendUDPData(toClient, packet);
        }
    }
}