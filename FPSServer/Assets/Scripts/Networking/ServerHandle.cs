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

    public static void PlayerInput(int fromClient, Packet packet) {
        int inputCount = packet.ReadInt();

        List<PlayerInput> inputs = new List<PlayerInput>();

        for (int i = 0; i < inputCount; i++) {
            PlayerInput input = new PlayerInput {
                tick = packet.ReadInt(),
                x = packet.ReadFloat(),
                y = packet.ReadFloat(),
                jumping = packet.ReadBool(),
                crouching = packet.ReadBool(),
            };
            
            inputs.Add(input);
        }
        
        Player player = Server.clients[fromClient].player;
        
        Debug.Log(inputs.Count);
    }
}