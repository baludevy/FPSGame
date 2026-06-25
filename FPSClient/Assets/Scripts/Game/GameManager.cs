using System.Collections.Generic;
using UnityEngine;


public class GameManager : MonoBehaviour {
    public static GameManager Instance;

    public static int serverTick;

    public static Dictionary<int, PlayerManager> players = new Dictionary<int, PlayerManager>();

    public GameObject localPlayerPrefab;
    public GameObject playerPrefab;

    private void Awake() {
        Instance = this;
    }

    private void Start() {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 500;
    }

    public void SpawnPlayer(int id, string username, Vector3 position, Quaternion rotation) {
        GameObject player;
        if (id == Client.Instance.myId) {
            player = Instantiate(localPlayerPrefab, position, rotation);
        }
        else {
            player = Instantiate(playerPrefab, position, rotation);
        }

        if (player == null) return;
        player.GetComponent<PlayerManager>().id = id;
        player.GetComponent<PlayerManager>().username = username;
        players.Add(id, player.GetComponent<PlayerManager>());
    }

    private void OnApplicationQuit() {
        Client.Instance.Disconnect();
    }
}