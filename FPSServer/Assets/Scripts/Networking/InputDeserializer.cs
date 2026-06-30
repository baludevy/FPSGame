using System.Collections.Generic;

public static class InputDeserializer {
    public static void PlayerInput(int fromClient, Packet packet) {
        uint inputSequence = packet.ReadUInt();

        uint serverTickAck = packet.ReadUInt();
        float clientSendTime = packet.ReadFloat();

        InputHeader header = new InputHeader {
            inputSequence = inputSequence,
            serverTickAck = serverTickAck,
            clientSendTime = clientSendTime,
        };

        if (!Server.clients.TryGetValue(fromClient, out Client client) || client?.player == null) {
            return;
        }

        client.player.monitor.ProcessHeader(header);

        int inputCount = packet.ReadByte();
        if (inputCount <= 0 || inputCount > 64) {
            return;
        }

        List<InputData> inputs = new List<InputData>(inputCount);

        for (int i = 0; i < inputCount; i++) {
            InputData input = new InputData {
                tick = packet.ReadUInt(),
                x = FloatCompressor.ShortToFloat(packet.ReadShort()),
                y = FloatCompressor.ShortToFloat(packet.ReadShort()),
                pitch = packet.ReadFloat(),
                yaw = packet.ReadFloat(),
                buttons = (Buttons)packet.ReadByte(),
            };
            inputs.Add(input);
        }

        client.player.inputBuffer.AddInputsToQueue(inputs);
    }
}