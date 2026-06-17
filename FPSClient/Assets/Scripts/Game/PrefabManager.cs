using UnityEngine;

public class PrefabManager : MonoBehaviour {
    public static PrefabManager Instance;

    private void Awake() {
        Instance = this;
    }
}