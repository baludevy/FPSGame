using UnityEngine;

public class WeaponController : MonoBehaviour
{
    public WeaponData data;
    public AudioSource audioSource;

    private bool canFire = true;

    public void Shoot()
    {
        if (!canFire) return;

        WeaponEffects.Instance.AddRecoil();
        PlayShootSound();

        canFire = false;
        LocalPlayer.Instance.invoker.Invoke(ResetFire, Mathf.RoundToInt(NetworkSettings.tickRate / data.fireRate));
    }

    private void PlayShootSound()
    {
        if (audioSource == null || data.shootSound == null) return;

        audioSource.pitch = 1f + Random.Range(-data.pitchVariation, data.pitchVariation);
        audioSource.PlayOneShot(data.shootSound);
    }

    private void ResetFire() => canFire = true;
}