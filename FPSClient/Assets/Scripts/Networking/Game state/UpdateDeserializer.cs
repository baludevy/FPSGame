public static class UpdateDeserializer {
    public static volatile uint latestTick;

    public static void GameUpdate(Packet packet) {
        uint serverTick = packet.ReadUInt();

        TimingInfo timingInfo = new TimingInfo {
            inputReceiveMargin = FloatCompressor.ShortToFloat(packet.ReadShort()),
            clientSendTimeAck = packet.ReadFloat(),
            clientReceiveTime = FixedClock.GetTime(),
            serverSendTime = packet.ReadFloat(),
            serverReceiveTime = packet.ReadFloat(),
        };

        UpstreamStatistics upstreamStatistics = new UpstreamStatistics {
            jitter = FloatCompressor.ShortToFloat(packet.ReadShort()),
            packetLoss = FloatCompressor.ShortToFloat(packet.ReadShort()),
        };

        NetStatisticsManager.UpdateStatistics(serverTick, timingInfo, upstreamStatistics);
        AdaptiveNetcode.Apply();
        if (InputPacer.Instance != null) {
            InputPacer.Instance.AdjustInputClock(NetStatistics.inputMargin, serverTick);
        }

        // dont do anything
        if (serverTick <= latestTick && latestTick != 0) {
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

        latestTick = serverTick;

        MovementState movementState = new MovementState {
            position = packet.ReadVector3(),
            velocity = packet.ReadVector3(),
            orientation = packet.ReadFloat(),
        };

        byte playerStateCount = packet.ReadByte();
        PlayerState[] playerStates = new PlayerState[playerStateCount];

        for (int i = 0; i < playerStateCount; i++) {
            playerStates[i] = new PlayerState {
                id = packet.ReadByte(),
                position = packet.ReadVector3(),
            };
        }

        WorldSnapshot snapshot = new WorldSnapshot {
            serverTick = serverTick,
            serverSendTime = timingInfo.serverSendTime,
            clientReceiveTime = timingInfo.clientReceiveTime,
            playerStatesCount = playerStateCount,
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
        latestTick = 0;
    }
}