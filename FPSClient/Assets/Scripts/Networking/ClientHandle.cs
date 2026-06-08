using System.Net;
using UnityEngine;

public class ClientHandle {
    public static void Welcome(Packet packet) {
        byte myId = packet.ReadByte();

        Client.Instance.myId = myId;
        Client.IsConnected = true;
        
        ClientSend.WelcomeReceived();
        
        Client.Instance.udp.Connect(((IPEndPoint)Client.Instance.tcp.socket.Client.LocalEndPoint).Port);
        
        Debug.Log("Connected.");
    }
    
    public static void SpawnPlayer(Packet _packet) {
        int id = _packet.ReadInt();
        string username = _packet.ReadString();
        Vector3 position = _packet.ReadVector3();
        Quaternion rotation = _packet.ReadQuaternion();

        GameManager.Instance.SpawnPlayer(id, username, position, rotation);
    }
}