using System.Collections.Generic;
using System.Net;
using UnityEngine;

public class ClientHandle {

    public static void Welcome(Packet packet) {
        int myId = packet.ReadInt();

        Client.Instance.myId = myId;
        Client.IsConnected = true;

        Client.Instance.udp.Connect(((IPEndPoint)Client.Instance.tcp.socket.Client.LocalEndPoint).Port);

        ConnectionManager.OnConnect();
    }

    public static void SpawnPlayer(Packet packet) {
        int id = packet.ReadInt();
        string username = packet.ReadString();
        Vector3 position = packet.ReadVector3();
        Quaternion rotation = packet.ReadQuaternion();

        GameManager.Instance.SpawnPlayer(id, username, position, rotation);
    }

    public static void SyncTick(Packet packet) {
        float clientSendTime = packet.ReadFloat();
        uint serverTick = packet.ReadUInt();

        TickSync.OnPong(clientSendTime, serverTick);
    }

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
            packetLoss = packet.ReadByte() / 100f,
        };

        MovementState movementState = new MovementState() {
            position = packet.ReadVector3(),
            velocity = packet.ReadVector3(), 
            orientation = packet.ReadFloat(),
        };

        byte playerStateCount = packet.ReadByte();
        List<PlayerState> playerStates = new List<PlayerState>();

        for (int i = 0; i < playerStateCount; i++) {
            PlayerState state = new PlayerState {
                id = packet.ReadByte(),
                position = packet.ReadVector3(),
            };

            playerStates.Add(state);
        }


        WorldSnapshot snapshot = new WorldSnapshot {
            serverTick = serverTick,
            serverSendTime = timingInfo.serverSendTime,
            clientReceiveTime = FixedClock.GetTime(),
            playerStates = playerStates,
        };

        GameUpdate update = new GameUpdate {
            serverTick = serverTick,
            timingInfo = timingInfo,
            upstreamStatistics = upstreamStatistics,
            movementState = movementState,
            worldSnapshot = snapshot,
        };
        
        UpdateManager.Instance.OnUpdateReceived(update);
    }


    public static void LagCompVisual(Packet packet) {
        Vector3 pos = packet.ReadVector3();

        // Object.Instantiate(PrefabManager.Instance.lagCompHitbox, pos, Quaternion.identity);
    }
}