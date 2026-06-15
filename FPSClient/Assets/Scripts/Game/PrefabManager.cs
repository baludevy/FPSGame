using System;
using UnityEngine;

public class PrefabManager : MonoBehaviour {
    public static PrefabManager Instance;

    public GameObject lagCompClient;
    public GameObject lagCompServer;

    private void Awake() {
        Instance = this;
    }
}