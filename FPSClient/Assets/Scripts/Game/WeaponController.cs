using UnityEngine;

public class WeaponController : MonoBehaviour {
    public void Shoot() {
        WeaponEffects.Instance.AddRecoil();
    }   
}