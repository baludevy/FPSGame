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

        ServerSend.SyncTick(fromClient, clientSendTime, FixedClock.tick);
    }

    public static void PlayerInput(int fromClient, Packet packet) {
        uint playerInputSequence = packet.ReadUInt();
        float clientSendTime = packet.ReadFloat();
        
        int inputCount = packet.ReadByte();

        List<InputData> inputs = new List<InputData>();

        for (int i = 0; i < inputCount; i++) {
            InputData inputData = new InputData {
                tick = packet.ReadUInt(),
                renderTick = packet.ReadFloat(),
                x = packet.ReadFloat(),
                y = packet.ReadFloat(),
                pitch = packet.ReadFloat(),
                yaw = packet.ReadFloat(),
                jumping = packet.ReadBool(),
                crouching = packet.ReadBool(),
                shoot = packet.ReadBool(),
            };

            inputs.Add(inputData);
        }

        Player player = Server.clients[fromClient].player;

        player.inputBuffer.AddInputsToQueue(inputs, playerInputSequence, clientSendTime);
    }
}