using UnityEngine;

public sealed class FrametimeMonitor : MonoBehaviour {
    public static volatile float lastFrametime;
    private void Update() {
        lastFrametime = Time.unscaledDeltaTime;
    }
}