using UnityEngine;

public sealed class FrametimeMonitor : MonoBehaviour {
    public static float meanFrametime;
    public static float frametimeStdDev; 
    public static float frametimeHigh;

    [SerializeField] private int   windowSize     = 240;
    [SerializeField] private float highPercentile = 0.95f;
    [SerializeField] private float maxSampleClamp = 0.1f;

    private float[] ring, scratch;
    private int count, head;
    
    public static float lastFrametime;

    private void Awake() { ring = new float[windowSize]; scratch = new float[windowSize]; }

    private void Update() {
        float ft = Time.unscaledDeltaTime;
        lastFrametime = ft;

        ring[head] = Mathf.Min(ft, maxSampleClamp);
        head = (head + 1) % windowSize;
        if (count < windowSize) count++;

        System.Array.Copy(ring, scratch, count);
        System.Array.Sort(scratch, 0, count);

        meanFrametime  = Percentile(scratch, count, 0.50f);
        frametimeHigh  = Percentile(scratch, count, highPercentile);
        
        frametimeStdDev = 0.5f * Mathf.Max(0f, frametimeHigh - meanFrametime);
    }

    private static float Percentile(float[] sorted, int n, float p) {
        if (n == 0) return 0f;
        float idx = p * (n - 1);
        int lo = Mathf.FloorToInt(idx);
        int hi = Mathf.Min(lo + 1, n - 1);
        return Mathf.Lerp(sorted[lo], sorted[hi], idx - lo);
    }
}