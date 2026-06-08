using System.Net;
using UnityEngine;

public class ClientHandle {
    public static void Welcome(Packet packet) {
        int myId = packet.ReadInt();
        int tick = packet.ReadInt();

        Client.Instance.myId = myId;
        Client.IsConnected = true;

        TickTimer.tick = tick + 100;
        
        ClientSend.WelcomeReceived();
        NetworkUIManager.Instance.DisableConnectUI();
        
        Client.Instance.udp.Connect(((IPEndPoint)Client.Instance.tcp.socket.Client.LocalEndPoint).Port);
        
        Debug.Log("Connected.");
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