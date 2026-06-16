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

        Server.clients[fromClient].SendIntoGame(username);
    }

    public static void SyncTick(int fromClient, Packet packet) {
        float clientSendTime = packet.ReadFloat();

        ServerSend.SyncTick(fromClient, clientSendTime, NetworkManager.tick);
    }

    public static void PlayerInput(int fromClient, Packet packet) {
        int inputCount = packet.ReadByte();

        float timestamp = packet.ReadFloat();

        List<PlayerInput> inputs = new List<PlayerInput>();

        for (int i = 0; i < inputCount; i++) {
            PlayerInput input = new PlayerInput {
                tick = packet.ReadUInt(),
                renderTick = packet.ReadFloat(),
                x = packet.ReadFloat(),
                y = packet.ReadFloat(),
                yaw = packet.ReadFloat(),
                pitch = packet.ReadFloat(),
                jumping = packet.ReadBool(),
                crouching = packet.ReadBool(),
                shoot = packet.ReadBool(),
            };

            inputs.Add(input);
        }

        Player player = Server.clients[fromClient].player;

        player.inputBuffer.AddInputsToQueue(inputs, timestamp);
    }
}