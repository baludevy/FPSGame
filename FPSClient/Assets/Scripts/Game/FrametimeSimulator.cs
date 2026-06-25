using UnityEngine;

public class FrametimeSimulator : MonoBehaviour {
    [Header("Frametime Settings (milliseconds)")] [Tooltip("Base frametime target in ms (e.g. 16.6 = 60fps)")]
    public float baseFrametimeMs = 16.6f;

    [Tooltip("Maximum random spike added on top of base frametime")]
    public float maxSpikeMs = 50f;

    [Tooltip("How often (in seconds) a spike occurs")]
    public float spikeInterval = 2f;

    [Tooltip("Duration of a spike in seconds")]
    public float spikeDuration = 0.3f;

    [Header("Noise Settings")] [Tooltip("Amount of continuous random noise added every frame (ms)")]
    public float noiseAmplitudeMs = 3f;

    [Tooltip("Speed of Perlin noise variation")]
    public float noiseSpeed = 1.5f;

    [Header("Debug")] public bool showDebugLog = false;

    private float _spikeTimer = 0f;
    private bool _isSpiking = false;
    private float _spikeDurationTimer = 0f;
    private float _currentSpikeMs = 0f;
    private float _noiseOffset;

    void Start() {
        _noiseOffset = Random.Range(0f, 100f);
    }

    void Update() {
        // --- Spike logic ---
        _spikeTimer += Time.unscaledDeltaTime;

        if (!_isSpiking && _spikeTimer >= spikeInterval) {
            _isSpiking = true;
            _spikeDurationTimer = 0f;
            _currentSpikeMs = Random.Range(maxSpikeMs * 0.5f, maxSpikeMs);
            _spikeTimer = 0f;
        }

        if (_isSpiking) {
            _spikeDurationTimer += Time.unscaledDeltaTime;
            if (_spikeDurationTimer >= spikeDuration)
                _isSpiking = false;
        }

        float noiseValue = Mathf.PerlinNoise(Time.unscaledTime * noiseSpeed + _noiseOffset, 0f);
        float jitterMs = (noiseValue * 2f - 1f) * noiseAmplitudeMs;

        float totalDelayMs = baseFrametimeMs + jitterMs + (_isSpiking ? _currentSpikeMs : 0f);
        totalDelayMs = Mathf.Max(0f, totalDelayMs);


        double delaySeconds = totalDelayMs / 1000.0;
        double start = Time.realtimeSinceStartupAsDouble;
        while (Time.realtimeSinceStartupAsDouble - start < delaySeconds) {
        }
    }

    void OnGUI() {
        float fps = 1f / Time.unscaledDeltaTime;
        float dtMs = Time.unscaledDeltaTime * 1000f;

        GUI.Label(new Rect(10, 10, 300, 25), $"FPS: {fps:F1}");
        GUI.Label(new Rect(10, 30, 300, 25), $"Frame Time: {dtMs:F2} ms");
        GUI.Label(new Rect(10, 50, 300, 25), $"Spiking: {_isSpiking}");
    }
}