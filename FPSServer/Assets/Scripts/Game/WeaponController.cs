using System.Collections.Generic;
using UnityEngine;

public class WeaponController : MonoBehaviour {
    public Player player;
    public WeaponData data;

    private bool canFire = true;

    public void Shoot(PlayerInput input, LagCompensation lagCompensation) {
        if (!canFire) return;

        Debug.Log("shoot");

        Vector3 origin = player.camera.position;
        Vector3 direction = player.camera.forward;

        Debug.DrawRay(origin, direction * 1000f, Color.red, 1f);

        WorldSnapshot rewoundSnapshot = lagCompensation.GetRewoundSnapshot(input.renderTick);

        List<(Player player, Vector3 originalPosition)> moved = new List<(Player, Vector3)>();

        foreach (PlayerState targetState in rewoundSnapshot.playerStates) {
            if (targetState.id == player.id) continue;

            if (!Server.clients.TryGetValue(targetState.id, out Client client) || client.player == null) continue;

            moved.Add((client.player, client.player.transform.position));
            client.player.transform.position = targetState.position;
        }

        Physics.SyncTransforms();

        Player hitPlayer = null;

        if (Physics.Raycast(origin, direction, out RaycastHit hit, 1000f)) {
            hitPlayer = hit.transform.GetComponent<Player>();

            if (hitPlayer != null) {
                Debug.Log($"hit player {hitPlayer.id}");
            }
        }

        foreach ((Player targetPlayer, Vector3 originalPosition) in moved) {
            targetPlayer.transform.position = originalPosition;
        }

        Physics.SyncTransforms();

        if (hitPlayer != null) {
            hitPlayer.transform.position = Vector3.zero;
        }

        canFire = false;
        player.invoker.Invoke(ResetFire, Mathf.RoundToInt(NetworkSettings.tickRate / data.fireRate));
    }

    private void ResetFire() => canFire = true;
}