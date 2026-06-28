using System.Collections.Generic;
using System.Net;
using UnityEngine;

public class ClientHandle {

    public static void Welcome(Packet packet) {
        int myId = packet.ReadInt();

        Client.Instance.myId = myId;
        
        byte[] token = packet.ReadBytes(NetProtocol.tokenLength);
        Client.Instance.sessionToken = token;

        Client.Instance.udp.Connect(((IPEndPoint)Client.Instance.tcp.socket.Client.LocalEndPoint).Port);

        NetworkManager.Instance.NotifyConnected();
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
        UpdateDeserializer.GameUpdate(packet);
    }


    public static void LagCompVisual(Packet packet) {
        Vector3 pos = packet.ReadVector3();

        // Object.Instantiate(PrefabManager.Instance.lagCompHitbox, pos, Quaternion.identity);
    }
}