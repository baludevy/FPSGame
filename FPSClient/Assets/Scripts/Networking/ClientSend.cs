using System.Collections.Generic;
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

    public static void MeasureRTT(long timestamp) {
        using (Packet packet = new Packet((int)ClientPackets.measureRtt)) {
            packet.Write(timestamp);
            
            SendTCPData(packet);
        }
    }
    
    public static void SyncTick(long timestamp) {
        using (Packet packet = new Packet((int)ClientPackets.syncTick)) {
            packet.Write(timestamp);

            SendTCPData(packet);
        }
    }

    public static void PlayerInput(List<PlayerInput> inputs) {
        using (Packet packet = new Packet((int)ClientPackets.playerInput)) {
            packet.Write(inputs.Count);
            
            foreach (PlayerInput input in inputs) {
                packet.Write(input.tick);
                packet.Write(input.x);
                packet.Write(input.y);
                packet.Write(input.orientation);
                packet.Write(input.jumping);
                packet.Write(input.crouching);
            }
            
            SendUDPData(packet);
        }
    }
}