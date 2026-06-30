using System.Net;
using UnityEngine;

public class ClientHandle {
    public static void Welcome(Packet packet) {
        int myId = packet.ReadInt();
        Client.Instance.myId = myId;

        byte[] token = packet.ReadBytes(NetProtocol.tokenLength);
        Client.Instance.sessionToken = token;

        Debug.Log($"Welcome received, assigned id {myId}");

        int localPort = ((IPEndPoint)Client.Instance.tcp.socket.Client.LocalEndPoint).Port;
        Client.Instance.udp.Connect(localPort);

        ClientSend.WelcomeReceived();
    }
    
    public static void UdpConfirmed(Packet packet) {
        packet.ReadInt();

        Debug.Log("UDP bind confirmed by server");

        Client.Instance.udp.StopPingRetry();
        NetworkManager.Instance.NotifyConnected();
    }

    public static void SpawnPlayer(Packet packet) {
        int id = packet.ReadInt();
        string username = packet.ReadString();
        Vector3 position = packet.ReadVector3();
        Quaternion rotation = packet.ReadQuaternion();

        GameManager.Instance.SpawnPlayer(id, username, position, rotation);
    }

    public static void GameUpdate(Packet packet) {
        UpdateDeserializer.GameUpdate(packet);
    }
}