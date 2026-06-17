using UnityEngine;

public static class ConnectionManager {
    public static void OnConnect() {
        ClientSend.WelcomeReceived();
        NetworkUIManager.Instance.DisableConnectUI();

        FixedClock.Reset();
        TickSync.StartSync();

        Debug.Log("Connected.");
    }

    public static void OnDisconnect() {
        foreach (PlayerManager player in GameManager.players.Values) {
            Object.Destroy(player.gameObject);
        }

        GameManager.players.Clear();

        NetworkUIManager.Instance.EnableConnectUI();
        CursorManager.EnableCursor();
        
        ConnectionStatistics.Reset();
        SnapshotManager.Instance.Reset();
        
        FixedClock.Reset();

        Debug.Log("Disconnected :(");
    }
}