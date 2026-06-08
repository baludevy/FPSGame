public class ServerSend {
    private static void SendTCPData(int toClient, Packet packet) {
        packet.WriteLength();
        Server.Clients[toClient].tcp.SendData(packet);
    }

    private static void SendTCPDataToAll(Packet packet) {
        packet.WriteLength();
        for (int i = 1; i <= Server.MaxPlayers; i++) {
            Server.Clients[i].tcp.SendData(packet);
        }
    }

    private static void SendTCPDataToAllExcept(int exceptClient, Packet packet) {
        packet.WriteLength();
        for (int i = 1; i <= Server.MaxPlayers; i++) {
            if (i != exceptClient) {
                Server.Clients[i].tcp.SendData(packet);
            }
        }
    }

    private static void SendUDPData(int toClient, Packet packet) {
        packet.WriteLength();
        Server.Clients[toClient].udp.SendData(packet);
    }

    private static void SendUDPDataToAll(Packet packet) {
        packet.WriteLength();
        for (int i = 1; i <= Server.MaxPlayers; i++) {
            Server.Clients[i].udp.SendData(packet);
        }
    }

    private static void SendUDPDataToAllExcept(int exceptClient, Packet packet) {
        packet.WriteLength();
        for (int i = 1; i <= Server.MaxPlayers; i++) {
            if (i != exceptClient) {
                Server.Clients[i].udp.SendData(packet);
            }
        }
    }


    public static void Welcome(int toClient) {
        using (Packet packet = new Packet((int)ServerPackets.welcome)) {
            packet.Write(toClient);

            SendTCPData(toClient, packet);
        }
    }
    
    public static void SpawnPlayer(int toClient, Player player) {
        using (Packet _packet = new Packet((int)ServerPackets.spawnPlayer)) {
            _packet.Write(player.id);
            _packet.Write(player.username);
            _packet.Write(player.transform.position);
            _packet.Write(player.transform.rotation);

            SendTCPData(toClient, _packet);
        }
    }
}