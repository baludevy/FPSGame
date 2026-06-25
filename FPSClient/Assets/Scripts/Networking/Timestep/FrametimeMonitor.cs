using UnityEngine;

public sealed class FrametimeMonitor : MonoBehaviour {
    public FrametimeGraph graph;
    public static volatile float lastFrametime;
    private void Update() {
        lastFrametime = Time.unscaledDeltaTime;
        
        graph.AddSample(Time.unscaledDeltaTime);
    }
}