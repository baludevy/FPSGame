using System.Collections.Generic;

public class ClientSend {
    private static void SendTCPData(Packet packet) {
        packet.WriteLength();

        NetStatistics.packetsSent++;
        NetStatistics.bytesSent += packet.Length();

        Client.Instance.tcp.SendData(packet);
    }

    private static void SendUDPData(Packet packet) {
        packet.WriteLength();

        NetStatistics.packetsSent++;
        NetStatistics.bytesSent += packet.Length();

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


    public static void PlayerInput(uint sequence, List<InputData> inputs) {
        using (Packet packet = new Packet((int)ClientPackets.playerInput)) {
            packet.Write(sequence);
            packet.Write(FixedClock.GetTime());

            packet.Write((byte)inputs.Count);

            foreach (InputData input in inputs) {
                packet.Write(input.tick);
                packet.Write(input.renderTick);
                packet.Write(FloatCompressor.FloatToShort(input.x));
                packet.Write(FloatCompressor.FloatToShort(input.y));
                packet.Write(input.pitch);
                packet.Write(input.yaw);
                packet.Write((byte)input.buttons);
            }

            SendUDPData(packet);
        }
    }
}