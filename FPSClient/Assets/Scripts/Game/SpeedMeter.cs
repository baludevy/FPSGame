using System;
using TMPro;
using UnityEngine;

public class SpeedMeter : MonoBehaviour {
    private float speed;
    private TMP_Text counter;

    private void Start()
    {
        counter = GetComponent<TMP_Text>();
    }

    void Update() {
        speed = PlayerMovement.Instance.rb.velocity.magnitude;
    }

    void OnGUI() {
        counter.text = $"vel: {(int)speed} m/s";
    }
}