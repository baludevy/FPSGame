using UnityEngine;


public class NetworkManager : MonoBehaviour {
    public static NetworkManager Instance;
    
    private void Awake() {
        Instance = this;
    }

    private void Start() {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = NetworkSettings.tickRate * 2;

        Server.Start(10, 42069);
    }

    private void OnApplicationQuit() {
        Server.Stop();
    }
}