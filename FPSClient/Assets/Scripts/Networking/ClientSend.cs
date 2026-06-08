using UnityEngine;

public class ClientSend {
    private static void SendTCPData(Packet packet) {
        packet.WriteLength();

        Client.Instance.tcp.SendData(packet);
    }

    private static void SendUDPData(Packet packet) {
        packet.WriteLength();

        Client.Instance.udp.SendData(packet);
    }
    
    public static void WelcomeReceived() {
        using (Packet packet = new Packet((int)ClientPackets.welcomeReceived)) {
            packet.Write(Client.Instance.myId);

            packet.Write(NetworkUIManager.Instance.usernameField.text != ""
                ? NetworkUIManager.Instance.usernameField.text
                : $"Player{Client.Instance.myId}");
            SendTCPData(packet);
        }
    }

    public static void PlayerPosition(Vector3 position) {
        using (Packet packet = new Packet((int)ClientPackets.playerPosition)) {
            packet.Write(position);
            
            SendUDPData(packet);
        }
    }
}