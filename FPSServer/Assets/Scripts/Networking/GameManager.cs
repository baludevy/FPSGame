using UnityEngine;

public class GameManager : FixedBehaviour {
    public static GameManager Instance;

    public GameObject playerPrefab;

    public LagCompensation lagCompensation = new LagCompensation();
    private WorldSnapshot currentSnapshot;

    private void Awake() {
        if (Instance == null) {
            Instance = this;
        }
        else {
            Destroy(gameObject);
        }
    }

    public override void UpdateBeforeTick() {
    }

    public override void UpdateFixed() {
        foreach (Client client in Server.clients.Values) {
            if (client.player != null) {
                InputData inputData = client.player.inputBuffer.GetInputFromQueue(FixedClock.tick);
                client.player.HandleInput(inputData);
            }
        }

        Physics.SyncTransforms();
        Physics.Simulate(NetworkSettings.tickTime);

        currentSnapshot = GetWorldSnapshot();

        lagCompensation.SaveSnapshot(currentSnapshot);
        lagCompensation.Update();

        foreach (Client client in Server.clients.Values) {
            if (client.player != null) {
                SendPlayerUpdate(client.player.id, currentSnapshot);
            }
        }
    }

    public override void UpdateAfterTick() {
    }

    private void SendPlayerUpdate(int toClient, WorldSnapshot sharedSnapshot) {
        GameUpdate update = new GameUpdate();
        Player toPlayer = Server.clients[toClient].player;

        update.serverTick = FixedClock.tick;

        update.timingInfo = toPlayer.inputBuffer.GetTimingInfo();

        update.upstreamStatistics = toPlayer.inputBuffer.GetUpstreamStatistics();

        update.movementState = new MovementState() {
            position = toPlayer.GetState().position,
            velocity = toPlayer.movement.GetRb().velocity,
            orientation = toPlayer.movement.orientation.eulerAngles.y,
        };

        update.worldSnapshot = sharedSnapshot;

        ServerSend.GameUpdate(toClient, update);
    }


    private WorldSnapshot GetWorldSnapshot() {
        WorldSnapshot snapshot = new WorldSnapshot() {
            tick = FixedClock.tick,
        };

        foreach (Client client in Server.clients.Values) {
            Player player = client.player;

            if (player != null) {
                snapshot.playerStates.Add(player.GetState());
            }
        }

        return snapshot;
    }

    private WorldSnapshot GetWorldSnapshotForPlayer(int forPlayer) {
        WorldSnapshot snapshot = new WorldSnapshot {
            tick = FixedClock.tick,
        };

        foreach (Client client in Server.clients.Values) {
            Player player = client.player;

            if (player != null || player.id != forPlayer) {
                snapshot.playerStates.Add(player.GetState());
            }
        }

        return snapshot;
    }

    public Player InstantiatePlayer() {
        return Instantiate(playerPrefab, Vector3.zero, Quaternion.identity).GetComponent<Player>();
    }
}