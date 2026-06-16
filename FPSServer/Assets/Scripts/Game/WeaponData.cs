using UnityEngine;

[CreateAssetMenu(fileName = "WeaponData", menuName = "Weapon Data")]
public class WeaponData : ScriptableObject {
    public string weaponName;
    public float fireRate;
}