using UnityEngine;

public class DestroyObject : MonoBehaviour {
    public float time;

    private void Start() {
        Invoke(nameof(Destroy), time);
    }

    private void Destroy() {
        Destroy(gameObject);
    }
}