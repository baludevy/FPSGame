using UnityEngine;

public static class ConnectionManager {
    public static void OnConnect() {
        ClientSend.WelcomeReceived();
        NetworkUIManager.Instance.DisableConnectUI();

        RTTManager.SendSyncTickRequest();

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

        TickTimer.doTick = false;
        TickTimer.timeScale = 1f;

        TickTimer.tick = 0;
        SnapshotManager.clientRenderTick = 0;
        SnapshotManager.serverTick = 0;

        Debug.Log("Disconnected :(");
    }
}