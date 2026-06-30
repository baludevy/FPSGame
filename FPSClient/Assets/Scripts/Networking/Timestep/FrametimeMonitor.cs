using UnityEngine;

public sealed class FrametimeMonitor : MonoBehaviour {
    public static float meanFrametime;
    public static float frametimeStdDev;
    public static float frametimeHigh;

    [SerializeField] private int windowSize = 240;
    [SerializeField] private float highPercentile = 0.95f;
    [SerializeField] private float maxSampleClamp = 0.1f;

    private float[] ring;
    private int count, head;

    private const int buckets = 1000;
    private int[] histogram = new int[buckets];

    public static float lastFrametime;

    private void Awake() {
        ring = new float[windowSize];
    }

    private void Update() {
        float ft = Time.unscaledDeltaTime;
        lastFrametime = ft;

        float clampedFt = Mathf.Min(ft, maxSampleClamp);

        if (count == windowSize) {
            float oldest = ring[head];
            int oldBucket = Mathf.Clamp(Mathf.FloorToInt((oldest / maxSampleClamp) * (buckets - 1)), 0, buckets - 1);
            histogram[oldBucket]--;
        }

        ring[head] = clampedFt;
        head = (head + 1) % windowSize;
        if (count < windowSize) count++;

        int newBucket = Mathf.Clamp(Mathf.FloorToInt((clampedFt / maxSampleClamp) * (buckets - 1)), 0, buckets - 1);
        histogram[newBucket]++;

        meanFrametime = GetPercentileFromHistogram(0.50f);
        frametimeHigh = GetPercentileFromHistogram(highPercentile);

        frametimeStdDev = 0.5f * Mathf.Max(0f, frametimeHigh - meanFrametime);
    }

    private float GetPercentileFromHistogram(float percentile) {
        if (count == 0) return 0f;

        int targetCount = Mathf.RoundToInt(percentile * (count - 1));
        int currentCount = 0;

        for (int i = 0; i < buckets; i++) {
            currentCount += histogram[i];
            if (currentCount > targetCount) {
                return ((float)i / (buckets - 1)) * maxSampleClamp;
            }
        }

        return maxSampleClamp;
    }
}