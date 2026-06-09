using System;
using System.Collections.Generic;
using System.Net;
using UnityEngine;

public class ClientHandle {
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
        int serverTick = packet.ReadInt();

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
        int serverTick = packet.ReadInt();
        int bufferSlack = packet.ReadInt();

        int playerCount = packet.ReadInt();
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
            bufferSlack = bufferSlack,
            playerStates = states,
        };
        
        SnapshotManager.Instance.AddSnapshot(snapshot);
    }
}