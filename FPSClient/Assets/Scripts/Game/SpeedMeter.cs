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
            speed = new Vector3(LocalPlayer.Instance.movement.GetRb().velocity.x, 0,
                LocalPlayer.Instance.movement.GetRb().velocity.z).magnitude;
    }

    void OnGUI() {
        counter.text = $"vel: {(int)speed} m/s";
    }
}