using UnityEngine;

public class TimeScaler : MonoBehaviour {
    public static TimeScaler Instance { get; private set; }

    private int targetInpBufferOffset => NetworkSettings.targetInpBufferOffset;

    private float normalTimescale = 1f;
    private float minTimescale = 0.7f;
    private float maxTimescale = 1.05f;

    private float sensitivity = 0.015f;
    private float smoothing = 0.25f;

    private float baseThreshold = 1f;
    private float minThreshold = 0.3f;
    private float jitterInfluence = 2f;
    private float jitterDecay = 1.5f;
    private float stableLockTime = 2f;

    public int currentBufferOffset;
    private float targetTimescale = 1f;
    public float currentThreshold { get; private set; }

    private float jitter;
    private float stableDuration;
    private int lastBufferOffset;
    private float lastSampleTime;
    private bool hasSample;

    private void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        targetTimescale = normalTimescale;
        TickTimer.timeScale = normalTimescale;
    }

    public void AdjustClock(int bufferOffset) {
        currentBufferOffset = bufferOffset;

        float now = TickTimer.Instance.GetTime();
        float deltaTime = hasSample ? Mathf.Max(now - lastSampleTime, 0.0001f) : 0f;

        if (hasSample) {
            float instantJitter = Mathf.Abs(bufferOffset - lastBufferOffset);
            float k = 1f - Mathf.Exp(-jitterDecay * deltaTime);
            jitter = Mathf.Lerp(jitter, instantJitter, k);
        }

        lastBufferOffset = bufferOffset;
        lastSampleTime = now;
        hasSample = true;


        float lockIn = Mathf.Clamp01(stableDuration / stableLockTime);
        float floor = Mathf.Lerp(baseThreshold, minThreshold, lockIn);

        currentThreshold = floor + jitter * jitterInfluence;

        int deviation = bufferOffset - targetInpBufferOffset;
        bool withinThreshold = Mathf.Abs(deviation) <= currentThreshold;

        if (withinThreshold) stableDuration += deltaTime;
        else stableDuration = 0f;

        if (withinThreshold) {
            targetTimescale = normalTimescale;
        }
        else {
            float adjustment = -deviation * sensitivity;
            targetTimescale = Mathf.Clamp(normalTimescale + adjustment, minTimescale, maxTimescale);
        }

        TickTimer.timeScale = Mathf.Lerp(TickTimer.timeScale, targetTimescale, smoothing);
    }
}