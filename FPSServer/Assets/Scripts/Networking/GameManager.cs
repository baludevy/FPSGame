using System;
using UnityEngine;

public class GameManager : FixedBehaviour {
    public static GameManager Instance;

    public GameObject playerPrefab;

    public LagCompensation lagCompensation = new LagCompensation();

    private void Awake() {
        if (Instance == null)
            Instance = this;
        else {
            Destroy(gameObject);
        }
    }

    public override void UpdateFixed() {
        foreach (Client client in Server.clients.Values) {
            if (client.player != null) {
                PlayerInput input = client.player.inputBuffer.GetInputFromQueue(FixedClock.tick);

                if (input != null) {
                    client.player.HandleInput(input);

                    if (input.shoot) {
                        client.player.weaponController.Shoot(input, lagCompensation);
                    }
                }
            }
        }
        
        Physics.Simulate(NetworkSettings.tickTime);

        lagCompensation.SaveSnapshot(GetWorldSnapshot());
        lagCompensation.Update();

        foreach (Client client in Server.clients.Values) {
            if (client.player != null) {
                SendPlayerUpdate(client.player.id);
            }
        }
    }

    private void SendPlayerUpdate(int toClient) {
        GameUpdate update = new GameUpdate();

        Player toPlayer = Server.clients[toClient].player;

        update.serverTick = FixedClock.tick;
        update.serverSendTime = FixedClock.GetTime();
        update.inputBufferSize = toPlayer.inputBuffer.GetBufferSize();

        update.clientSendTime = toPlayer.inputBuffer.latestTimestamp;
        update.serverReceiveTime = toPlayer.inputBuffer.latestReceived;

        foreach (Client client in Server.clients.Values) {
            Player player = client.player;

            if (player == null) continue;

            if (player.id == toClient) {
                update.movementState = new MovementState() {
                    id = player.id,
                    position = player.transform.position,
                    orientation = player.movement.orientation.eulerAngles.y,
                    velocity = player.movement.rb.velocity
                };
            }
        }

        update.worldSnapshot = GetWorldSnapshot();

        ServerSend.GameUpdate(toClient, update);
    }

    private WorldSnapshot GetWorldSnapshot() {
        WorldSnapshot snapshot = new WorldSnapshot() {
            tick = FixedClock.tick,
        };

        foreach (Client client in Server.clients.Values) {
            Player player = client.player;

            if (player == null) continue;

            PlayerState playerState = new PlayerState {
                id = player.id,
                position = player.transform.position,
                crouching = player.movement.crouching
            };

            snapshot.playerStates.Add(playerState);
        }

        return snapshot;
    }

    private WorldSnapshot GetWorldSnapshotForPlayer(int forPlayer) {
        WorldSnapshot snapshot = new WorldSnapshot {
            tick = FixedClock.tick,
        };

        foreach (Client client in Server.clients.Values) {
            Player player = client.player;

            if (player == null || player.id == forPlayer) continue;

            PlayerState playerState = new PlayerState {
                id = player.id,
                position = player.transform.position,
                crouching = player.movement.crouching,
            };

            snapshot.playerStates.Add(playerState);
        }

        return snapshot;
    }

    public Player InstantiatePlayer() {
        return Instantiate(playerPrefab, Vector3.zero, Quaternion.identity).GetComponent<Player>();
    }
}