using UnityEngine;

public class WeaponController : MonoBehaviour {
    public WeaponData data;
    public AudioSource audioSource;

    private bool canFire = true;

    public void Shoot() {
        if (!canFire) return;

        WeaponEffects.Instance.AddRecoil();
        PlayShootSound();

        foreach (PlayerManager player in GameManager.players.Values) {
            if (player.id != Client.Instance.myId) {
                Object.Instantiate(PrefabManager.Instance.currentHitbox, player.transform.position,
                    Quaternion.identity);
            }
        }

        canFire = false;
        TickInvoker.Invoke(ResetFire, Mathf.RoundToInt(NetworkSettings.tickRate / data.fireRate));
    }

    private void PlayShootSound() {
        if (audioSource == null || data.shootSound == null) return;

        audioSource.pitch = 1f + Random.Range(-data.pitchVariation, data.pitchVariation);
        audioSource.PlayOneShot(data.shootSound);
    }

    private void ResetFire() => canFire = true;
}