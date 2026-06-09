using System;
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

    public static void PlayerPosition(Packet packet) {
        int id = packet.ReadInt();
        Vector3 position = packet.ReadVector3();

        GameManager.players[id].transform.position = position;
    }
}