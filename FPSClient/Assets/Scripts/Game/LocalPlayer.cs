using System;
using UnityEngine;

public class LocalPlayer : MonoBehaviour {
    public static LocalPlayer Instance;

    private void Awake() {
        invoker = new TickInvoker();

        if (Instance == null)
            Instance = this;
        else {
            Destroy(gameObject);
        }
    }

    public PlayerMovement movement;
    public TickInvoker invoker;
    public InputManager input;
    public WeaponController weaponController;
}