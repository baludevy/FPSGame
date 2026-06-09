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
        long timestamp = packet.ReadLong();

        RTTManager.ReceiveRTTResponse(timestamp);
    }

    public static void SyncTick(Packet packet) {
        long timestamp = packet.ReadLong();
        int serverTick = packet.ReadInt();

        long now = DateTime.UtcNow.Ticks;

        double rttMs = (now - timestamp) / (double)TimeSpan.TicksPerMillisecond;

        double oneWaySeconds = (rttMs * 0.5) / 1000.0;

        int latencyTicks = Mathf.CeilToInt(
            (float)(oneWaySeconds / NetworkSettings.tickTime)
        );

        int desiredTick = serverTick + latencyTicks + 1;

        TickTimer.tick = desiredTick;

        Debug.Log(
            $"RTT={rttMs:F1}ms | " +
            $"Server={serverTick} | " +
            $"Client={TickTimer.tick} | "
        );


        TickTimer.doTick = true;
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