using UnityEngine;

[CreateAssetMenu(fileName = "WeaponData", menuName = "Weapon Data")]
public class WeaponData : ScriptableObject {
    public string weaponName;
    public float fireRate;

    public AudioClip shootSound;
    [Range(0f, 0.3f)] public float pitchVariation = 0.1f;
}