using System.Collections.Generic;

public static class UpdateDeserializer {
    private static uint lastProcessedTick;

    public static void GameUpdate(Packet packet) {
        uint serverTick = packet.ReadUInt();

        TimingInfo timingInfo = new TimingInfo {
            inputReceiveMargin = FloatCompressor.ShortToFloat(packet.ReadShort()),
            clientSendTimeAck = packet.ReadFloat(),
            serverSendTime = packet.ReadFloat(),
            serverReceiveTime = packet.ReadFloat(),
        };

        UpstreamStatistics upstreamStatistics = new UpstreamStatistics {
            jitter = FloatCompressor.ShortToFloat(packet.ReadShort()),
            packetLoss = FloatCompressor.ShortToFloat(packet.ReadShort()),
        };

        NetStatisticsManager.UpdateStatistics(serverTick, FixedClock.GetTime(), timingInfo, upstreamStatistics);
        AdaptiveNetcode.Apply();
        InputPacer.AdjustInputClock(NetStatistics.inputMargin, serverTick);

        if (serverTick <= lastProcessedTick && lastProcessedTick != 0) {
            packet.ReadVector3();
            packet.ReadVector3();
            packet.ReadFloat();

            byte count = packet.ReadByte();
            for (int i = 0; i < count; i++) {
                packet.ReadByte();
                packet.ReadVector3();
            }
            return;
        }

        lastProcessedTick = serverTick;

        MovementState movementState = new MovementState {
            position = packet.ReadVector3(),
            velocity = packet.ReadVector3(),
            orientation = packet.ReadFloat(),
        };

        byte playerStateCount = packet.ReadByte();
        List<PlayerState> playerStates = new List<PlayerState>(playerStateCount);

        for (int i = 0; i < playerStateCount; i++) {
            playerStates.Add(new PlayerState {
                id = packet.ReadByte(),
                position = packet.ReadVector3(),
            });
        }

        WorldSnapshot snapshot = new WorldSnapshot {
            serverTick = serverTick,
            serverSendTime = timingInfo.serverSendTime,
            clientReceiveTime = FixedClock.GetTime(),
            playerStates = playerStates
        };

        UpdateManager.Instance.OnUpdateReceived(new GameUpdate {
            serverTick = serverTick,
            timingInfo = timingInfo,
            upstreamStatistics = upstreamStatistics,
            movementState = movementState,
            worldSnapshot = snapshot,
        });
    }

    public static void Reset() {
        lastProcessedTick = 0;
    }
}