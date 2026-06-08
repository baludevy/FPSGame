using UnityEngine;

public class ServerHandle {
    public static void WelcomeReceived(int fromClient, Packet packet) {
        int clientIdCheck = packet.ReadInt();
        string username = packet.ReadString();

        Debug.Log($"Client {username} with user id {clientIdCheck} connected.");
        if (fromClient != clientIdCheck) {
            MonoBehaviour.print(
                $"Player \"{username}\" (id: {fromClient}) has assumed the wrong client id ({clientIdCheck})!");
        }
        
        Server.clients[fromClient].SendIntoGame(username);
    }

    public static void PlayerPosition(int fromClient, Packet packet) {
        Vector3 position = packet.ReadVector3();

        Server.clients[fromClient].player.transform.position = position;
    }
}