using System.Collections.Generic;
using System.Net;
using UnityEngine;

public class ClientHandle {
    public static int packetsReceived;
    public static int bytesReceived;

    public static void Welcome(Packet packet) {
        int myId = packet.ReadInt();

        Client.Instance.myId = myId;
        Client.IsConnected = true;

        Client.Instance.udp.Connect(((IPEndPoint)Client.Instance.tcp.socket.Client.LocalEndPoint).Port);

        ConnectionManager.OnConnect();
    }

    public static void MeasureRTT(Packet packet) {
        double timestamp = packet.ReadDouble();

        RTTManager.ReceiveRTTResponse(timestamp);
    }

    public static void SyncTick(Packet packet) {
        double timestamp = packet.ReadDouble();
        uint serverTick = packet.ReadUInt();

        RTTManager.SyncTick(timestamp, serverTick);
    }

    public static void SpawnPlayer(Packet packet) {
        int id = packet.ReadInt();
        string username = packet.ReadString();
        Vector3 position = packet.ReadVector3();
        Quaternion rotation = packet.ReadQuaternion();

        GameManager.Instance.SpawnPlayer(id, username, position, rotation);
    }

    public static void WorldSnapshot(Packet packet) {
        uint serverTick = packet.ReadUInt();
        sbyte inputBufferOffset = packet.ReadSByte();

        float clientSendTime = packet.ReadFloat();
        float serverSendTime = packet.ReadFloat();
        float serverReceiveTime = packet.ReadFloat();

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
            };

            playerStates.Add(state);
        }

        WorldSnapshot snapshot = new WorldSnapshot {
            serverTick = serverTick,
            inputBufferOffset = inputBufferOffset,
            clientSendTime = clientSendTime,
            serverSendTime = serverSendTime,
            serverReceiveTime = serverReceiveTime,
            movementState = movementState,
            playerStates = playerStates,
        };

        SnapshotManager.Instance.OnSnapshotReceived(snapshot);
    }
}