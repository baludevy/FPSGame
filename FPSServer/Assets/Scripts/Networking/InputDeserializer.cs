using System.Collections.Generic;

public static class InputDeserializer {
    public static void PlayerInput(int fromClient, Packet packet) {
        uint playerInputSequence = packet.ReadUInt();
        float clientSendTime = packet.ReadFloat();

        int inputCount = packet.ReadByte();
        if (inputCount <= 0 || inputCount > 64) {
            return;
        }

        if (!Server.clients.TryGetValue(fromClient, out Client client) || client?.player == null) {
            return;
        }

        List<InputData> inputs = new List<InputData>(inputCount);

        for (int i = 0; i < inputCount; i++) {
            inputs.Add(new InputData {
                tick = packet.ReadUInt(),
                renderTick = packet.ReadFloat(),
                x = FloatCompressor.ShortToFloat(packet.ReadShort()),
                y = FloatCompressor.ShortToFloat(packet.ReadShort()),
                pitch = packet.ReadFloat(),
                yaw = packet.ReadFloat(),
                buttons = (Buttons)packet.ReadByte(),
            });
        }

        client.player.inputBuffer.AddInputsToQueue(inputs, playerInputSequence, clientSendTime);
    }
}