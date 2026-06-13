using UnityEngine;

public class TimeScaler : MonoBehaviour {
    public static TimeScaler Instance { get; private set; }

    private int targetBufferSlack => NetworkSettings.targetBufferSlack;
    private int threshold => Mathf.FloorToInt(targetBufferSlack * 0.5f);

    private float normalTimescale = 1f;
    private float minTimescale = 0.8f;
    private float maxTimescale = 1.2f;

    private float sensitivity = 0.01f;
    private float maxScaleDeviation = 0.07f;

    public int currentBufferSlack;

    private void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        TickTimer.timeScale = normalTimescale;
    }

    public void AdjustClock(int bufferSlack) {
        currentBufferSlack = bufferSlack;

        int deviation = bufferSlack - targetBufferSlack;

        if (Mathf.Abs(deviation) <= threshold) {
            TickTimer.timeScale = normalTimescale;
            return;
        }

        float adjustment = -deviation * sensitivity;
        adjustment = Mathf.Clamp(adjustment, -maxScaleDeviation, maxScaleDeviation);

        TickTimer.timeScale = Mathf.Clamp(
            normalTimescale + adjustment,
            minTimescale,
            maxTimescale
        );
    }
}