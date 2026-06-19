using System.Collections.Generic;
using UnityEngine;

public class WeaponController : MonoBehaviour {
    public Player player;
    public WeaponData data;
    private bool canFire = true;
    public bool enableLagComp = true;

    public void Shoot(InputData inputData, LagCompensation lagCompensation) {
        if (!canFire) return;
        Vector3 origin = player.playerCam.position;
        Vector3 direction = player.playerCam.forward;

        Debug.DrawRay(origin, direction * 1000f, Color.red, 1f);
        List<(Player player, Vector3 originalPosition)> moved = new List<(Player, Vector3)>();

        if (enableLagComp) {
            WorldSnapshot rewoundSnapshot = lagCompensation.GetRewoundSnapshot(inputData.renderTick);
            foreach (PlayerState targetState in rewoundSnapshot.playerStates) {
                if (targetState.id == player.id) continue;
                if (!Server.clients.TryGetValue(targetState.id, out Client client) || client.player == null) continue;

                moved.Add((client.player, client.player.transform.position));
                client.player.transform.position = targetState.position;
            }
        }


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

        canFire = false;
        player.invoker.Invoke(ResetFire, Mathf.RoundToInt(NetworkSettings.tickRate / data.fireRate));
    }

    private void ResetFire() => canFire = true;
}