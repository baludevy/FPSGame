using System.Collections.Generic;
using System.Linq;
using UnityEngine;


public class Player : MonoBehaviour {
    public int id;
    public string username;

    public InputBuffer InputBuffer;
    public PlayerMovement movement;

    public bool shoot;

    public void Initialize(int id, string username) {
        this.id = id;
        this.username = username;

        InputBuffer = new InputBuffer();
        InputBuffer.Initialize();
    }

    public void ProcessInput(PlayerInput input) {
        if (input.shoot && !shoot) Shoot(input);
        shoot = input.shoot;

        movement.SetInput(input);
        movement.AdvanceLogic();
    }

    public void Shoot(PlayerInput input) {
        foreach (Client client in Server.clients.Values) {
            Player player = client.player;

            if (player == null || player.id == id)
                continue;

            Vector3 rewoundPos = NetworkManager.Instance.lagCompensation.GetRewoundPosition(input.renderTick, player.id);

            ServerSend.LagCompDebug(id, rewoundPos);
            Debug.Log($"Player {player.id} rewound pos at tick {input.renderTick}: {rewoundPos}");
        }
    }
}