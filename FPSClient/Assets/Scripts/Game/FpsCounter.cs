using TMPro;
using UnityEngine;

public class FPSCounter : MonoBehaviour {
    private float deltaTime;
    private TMP_Text counter;

    private void Start() {
        counter = GetComponent<TMP_Text>();
    }

    void Update() {
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
    }

    void OnGUI() {
        int fps = (int)(1.0f / deltaTime);
        float msec = deltaTime * 1000.0f;

        counter.text = $"FPS: {fps} ({Mathf.Round(msec * 10) / 10} ms)";
    }
}