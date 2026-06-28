using System.Collections.Generic;
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

        Server.clients[fromClient].CompleteHandshake();
        Server.clients[fromClient].SendIntoGame(username);
    }

    public static void SyncTick(int fromClient, Packet packet) {
        float clientSendTime = packet.ReadFloat();

        ServerSend.SyncTick(fromClient, clientSendTime, FixedClock.tick);
    }

    public static void PlayerInput(int fromClient, Packet packet) {
        InputDeserializer.PlayerInput(fromClient, packet);
    }
}