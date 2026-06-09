using UnityEngine;

public class TimeScaler : MonoBehaviour
{
    public static TimeScaler Instance { get; private set; }
    
    private int targetBufferSlack => NetworkSettings.targetBufferSlack;
    [SerializeField] private int threshold = 1;
    
    [SerializeField] private float normalTimescale = 1f;
    [SerializeField] private float minTimescale = 0.8f;
    [SerializeField] private float maxTimescale = 1.2f;
    
    [SerializeField] private float sensitivity = 0.01f;
    [SerializeField] private float maxScaleDeviation = 0.07f;

    [SerializeField] private float smoothSpeed = 8f;

    private float currentScale;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        currentScale = normalTimescale;
    }

    public void AdjustClock(int bufferSlack)
    {
        if (bufferSlack == 0)
        {
            TickTimer.timeScale = Mathf.Clamp(normalTimescale + 0.05f, minTimescale, maxTimescale);
            return;
        }

        int deviation = bufferSlack - targetBufferSlack;

        if (Mathf.Abs(deviation) <= threshold)
        {
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

    private void Update()
    {
        TickTimer.timeScale = Mathf.Lerp(
            TickTimer.timeScale,
            currentScale,
            Time.deltaTime * smoothSpeed
        );
    }
}