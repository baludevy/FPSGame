using UnityEngine;


public class NetworkManager : MonoBehaviour {
    public static NetworkManager Instance;

    private float timer;
    public static int tick;

    public GameObject playerPrefab;

    public int movementPacketsReceivedInTick;

    private void Awake() {
        Instance = this;
    }

    private void Start() {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = NetworkSettings.tickRate;
        Server.Start(10, 42069);
    }

    private void Update() {
        timer += Time.deltaTime;
        while (timer >= NetworkSettings.tickTime) {
            timer -= NetworkSettings.tickTime;

            ProcessTick();
            tick++;
        }
    }

    private void ProcessTick() {
        ThreadManager.UpdateMain();

        //Forward physics simulation by one step


        foreach (Client client in Server.clients.Values) {
            if (client.player != null) {
                PlayerInput input = client.player.inputQueue.GetInputFromQueue(tick % NetworkSettings.inputBufferSize);

                if (input != null)
                    client.player.movement.SetInputs(input.x, input.y, input.orientation, input.jumping,
                        input.crouching);

                client.player.movement.Tick();

                ServerSend.PlayerPosition(client.player.id, client.player.transform.position);
            }
        }

        Physics.Simulate(NetworkSettings.tickTime);

        // Debug.Log(movementPacketsReceivedInTick);

        movementPacketsReceivedInTick = 0;
    }

    private void OnApplicationQuit() {
        Server.Stop();
    }

    public Player InstantiatePlayer() {
        return Instantiate(playerPrefab, Vector3.zero, Quaternion.identity).GetComponent<Player>();
    }
}