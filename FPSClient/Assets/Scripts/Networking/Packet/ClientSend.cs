using System.Collections.Generic;

public class ClientSend {
    public static int packetsSent;
    public static int bytesSent;

    private static void SendTCPData(Packet packet) {
        packet.WriteLength();

        packetsSent++;
        bytesSent += packet.Length();

        Client.Instance.tcp.SendData(packet);
    }

    private static void SendUDPData(Packet packet) {
        packet.WriteLength();

        packetsSent++;
        bytesSent += packet.Length();

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

    public static void SyncTick(float clientSendTime) {
        using (Packet packet = new Packet((int)ClientPackets.syncTick)) {
            packet.Write(clientSendTime);

            SendUDPData(packet);
        }
    }

    public static void PlayerInput(List<InputData> inputs) {
        using (Packet packet = new Packet((int)ClientPackets.playerInput)) {
            packet.Write((byte)inputs.Count);

            packet.Write(FixedClock.GetTime());

            foreach (InputData input in inputs) {
                packet.Write(input.tick);
                packet.Write(input.renderTick);
                packet.Write(input.x);
                packet.Write(input.y);
                packet.Write(input.pitch);
                packet.Write(input.yaw);
                packet.Write(input.jumping);
                packet.Write(input.crouching);
                packet.Write(input.shoot);
            }

            SendUDPData(packet);
        }
    }
}