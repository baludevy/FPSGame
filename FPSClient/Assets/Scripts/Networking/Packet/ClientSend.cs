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

    private static string ResolveUsername() {
        string typed = NetworkUIManager.Instance.usernameField.text;
        if (typed != "") {
            return typed;
        }

        return $"Player{Client.Instance.myId}";
    }

    public static void WelcomeReceived() {
        using (Packet packet = new Packet((int)ClientPackets.welcomeReceived)) {
            packet.Write(Client.Instance.myId);
            packet.Write(ResolveUsername());
            SendTCPData(packet);
        }
    }

    public static void PlayerInput(InputHeader header, List<InputData> inputs) {
        using (Packet packet = new Packet((int)ClientPackets.playerInput)) {
            packet.Write(header.inputSequence); // 4 bytes
            
            packet.Write(header.serverTickAck); // 4 bytes
            packet.Write(header.clientSendTime); // 4 bytes

            packet.Write((byte)inputs.Count); // 1 byte

            foreach (InputData input in inputs) {
                packet.Write(input.tick); // 4 bytes
                packet.Write(FloatCompressor.FloatToShort(input.x)); // 2 bytes
                packet.Write(FloatCompressor.FloatToShort(input.y)); // 2 bytes
                packet.Write(input.pitch); // 4 bytes
                packet.Write(input.yaw); // 4 bytes
                packet.Write((byte)input.buttons); // 1 byte
            }

            SendUDPData(packet);
        }
    }
}