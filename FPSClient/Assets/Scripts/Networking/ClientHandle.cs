using System;
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

        ClientSend.WelcomeReceived();
        NetworkUIManager.Instance.DisableConnectUI();

        Client.Instance.udp.Connect(((IPEndPoint)Client.Instance.tcp.socket.Client.LocalEndPoint).Port);

        RTTManager.SendSyncTickRequest();

        Debug.Log("Connected.");
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

        byte playerCount = packet.ReadByte();
        List<PlayerState> states = new List<PlayerState>();

        for (int i = 0; i < playerCount; i++) {
            PlayerState state = new PlayerState {
                id = packet.ReadInt(),
                position = packet.ReadVector3(),
                velocity = packet.ReadVector3(),
                orientation = packet.ReadFloat()
            };
        
            states.Add(state);
        }
        
        WorldSnapshot snapshot = new WorldSnapshot {
            serverTick = serverTick,
            inputBufferOffset = inputBufferOffset,
            clientSendTime = clientSendTime,
            serverSendTime = serverSendTime,
            serverReceiveTime = serverReceiveTime,
            playerStates = states,
        };
        
        SnapshotManager.Instance.OnSnapshotReceived(snapshot);
    }
}