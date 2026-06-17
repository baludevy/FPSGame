using UnityEngine;

public class PrefabManager : MonoBehaviour {
    public static PrefabManager Instance;

    public GameObject currentHitbox;
    public GameObject lagCompHitbox;

    private void Awake() {
        Instance = this;
    }
}