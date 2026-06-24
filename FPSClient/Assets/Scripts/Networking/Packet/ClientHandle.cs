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
            inputReceiveMargin = packet.ReadFloat(),
            clientSendTimeAck = packet.ReadFloat(),
            serverSendTime = packet.ReadFloat(),
            serverReceiveTime = packet.ReadFloat(),
        };

        UpstreamStatistics upstreamStatistics = new UpstreamStatistics {
            jitter = packet.ReadFloat(),
            packetLoss = packet.ReadFloat(),
        };

        MovementState movementState = new MovementState() {
            id = packet.ReadInt(),
            position = packet.ReadVector3(),
            orientation = packet.ReadFloat(),
            velocity = packet.ReadVector3(),
        };

        byte playerStateCount = packet.ReadByte();
        List<PlayerState> playerStates = new List<PlayerState>();

        for (int i = 0; i < playerStateCount; i++) {
            PlayerState state = new PlayerState {
                id = packet.ReadInt(),
                position = packet.ReadVector3(),
                crouching = packet.ReadBool(),
            };

            playerStates.Add(state);
        }


        WorldSnapshot snapshot = new WorldSnapshot {
            serverTick = serverTick,
            playerStates = playerStates,
        };

        GameUpdate update = new GameUpdate {
            serverTick = serverTick,
            timingInfo = timingInfo,
            upstreamStatistics = upstreamStatistics,
            movementState = movementState,
            worldSnapshot = snapshot,
        };
        
        SnapshotManager.Instance.OnUpdateReceived(update);
    }


    public static void LagCompVisual(Packet packet) {
        Vector3 pos = packet.ReadVector3();

        // Object.Instantiate(PrefabManager.Instance.lagCompHitbox, pos, Quaternion.identity);
    }
}