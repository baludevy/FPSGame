using System;

public static class TcpFraming {
    public static bool Process(Packet receivedData, byte[] data, Func<bool> onOverflow, Action<byte[]> dispatch) {
        receivedData.SetBytes(data);

        if (receivedData.Length() > NetProtocol.maxAccumulatedBuffer)
            return onOverflow();

        if (receivedData.UnreadLength() < 4) return true;

        int packetLength = receivedData.ReadInt();
        if (packetLength <= 0) return true;
        if (packetLength > NetProtocol.maxPacketSize) return onOverflow();

        while (packetLength > 0 && packetLength <= receivedData.UnreadLength()) {
            dispatch(receivedData.ReadBytes(packetLength));
            packetLength = 0;
            if (receivedData.UnreadLength() >= 4) {
                packetLength = receivedData.ReadInt();
                if (packetLength <= 0) return true;
                if (packetLength > NetProtocol.maxPacketSize) return onOverflow();
            }
        }

        return packetLength <= 1;
    }
}