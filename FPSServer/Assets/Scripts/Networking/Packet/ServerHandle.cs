using UnityEngine;

public class ServerHandle {
    public static void WelcomeReceived(int fromClient, Packet packet) {
        int clientIdCheck = packet.ReadInt();
        string username = packet.ReadString();

        if (fromClient != clientIdCheck) {
            Debug.LogWarning($"Client {fromClient} claimed wrong id {clientIdCheck} (name {username})");
            return;
        }

        Debug.Log($"Received welcome from Client {fromClient} ({username})");

        if (Server.clients.TryGetValue(fromClient, out Client client)) {
            client.username = username;
            client.MarkWelcomeAcked();
        }
    }

    public static void PlayerInput(int fromClient, Packet packet) {
        InputDeserializer.PlayerInput(fromClient, packet);
    }
}