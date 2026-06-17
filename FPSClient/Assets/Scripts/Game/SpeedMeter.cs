using TMPro;
using UnityEngine;

public class SpeedMeter : MonoBehaviour {
    private float speed;
    private TMP_Text counter;

    private void Start() {
        counter = GetComponent<TMP_Text>();
    }

    void Update() {
        if (LocalPlayer.Instance != null)
            speed = LocalPlayer.Instance.movement.rb.velocity.magnitude;
    }

    void OnGUI() {
        counter.text = $"vel: {(int)speed} m/s";
    }
}