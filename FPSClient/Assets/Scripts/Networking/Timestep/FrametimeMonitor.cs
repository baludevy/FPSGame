using UnityEngine;

public sealed class FrametimeMonitor : MonoBehaviour {
    public static float lastFrametime;
    public static float meanFrametime;
    public static float frametimeStdDev;

    private float smoothing = 0.05f;
    private float meanSq;
    private bool initialized;

    private void Update() {
        float ft = Time.unscaledDeltaTime;
        lastFrametime = ft;

        if (!initialized) {
            meanFrametime = ft;
            meanSq = 0f;
            initialized = true;
            return;
        }

        float delta = ft - meanFrametime;
        meanFrametime += smoothing * delta;

        float dev = ft - meanFrametime;
        meanSq = (1f - smoothing) * meanSq + smoothing * (dev * dev);

        frametimeStdDev = Mathf.Sqrt(meanSq);
    }
}