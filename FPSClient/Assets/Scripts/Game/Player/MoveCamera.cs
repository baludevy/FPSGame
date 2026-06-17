using UnityEngine;

public class MoveCamera : MonoBehaviour {
    public Transform player;

    public Vector3 offset;
    public Vector3 crouchOffset;
    public Vector3 bobOffset;

    public bool smooth;
    private Vector3 desiredBob;

    private float bobSpeed = 15f;
    private float bobMultiplier = 0.5f;

    private float baseFov = 90f;
    private float targetFov;

    private float fovVelocity;
    private float speedThreshold = 12f;
    private float maxSpeedForFov = 35f;
    
    private float fovSmoothTime = 0.2f;
    private float maxFovOffset = 15f;

    public PlayerMovement playerMovement;
    public Camera cam;

    private void Start() {
        if (cam != null) baseFov = cam.fieldOfView;
    }

    private void Update() {
        if (smooth) {
            transform.position = Vector3.Lerp(transform.position,
                player.position + bobOffset + crouchOffset + offset, NetworkSettings.tickTime * 5);
        }
        else {
            transform.position = player.position + bobOffset + crouchOffset + offset;
        }

        UpdateFov();
        UpdateBob();
    }

    private void UpdateFov() {
        if (baseFov <= 0) {
            baseFov = cam.fieldOfView;
            return;
        }

        float currentSpeed = playerMovement.GetRb().velocity.magnitude;

        float speedFactor = Mathf.InverseLerp(speedThreshold, maxSpeedForFov, currentSpeed);
        float speedFovOffset = speedFactor * maxFovOffset;

        bool isWallRunning = playerMovement.IsWallRunning();

        float wallrunFovOffset = isWallRunning ? 10 : 0f;

        targetFov = baseFov + Mathf.Max(speedFovOffset, wallrunFovOffset);
        targetFov = Mathf.Clamp(targetFov, baseFov, baseFov + maxFovOffset);

        cam.fieldOfView = Mathf.SmoothDamp(cam.fieldOfView, targetFov, ref fovVelocity, fovSmoothTime);
    }

    public void BobOnce(Vector3 bobDirection) {
        Vector3 vector = ClampVector(bobDirection * 0.15f, -3f, 3f);
        desiredBob = vector * bobMultiplier;

        if (MoveWeapon.Instance != null)
            MoveWeapon.Instance.BobOnce(-vector);
    }

    private void UpdateBob() {
        desiredBob = Vector3.Lerp(desiredBob, Vector3.zero, Time.deltaTime * bobSpeed * 0.5f);
        bobOffset = Vector3.Lerp(bobOffset, desiredBob, Time.deltaTime * bobSpeed);
    }

    private Vector3 ClampVector(Vector3 vec, float min, float max) {
        return new Vector3(Mathf.Clamp(vec.x, min, max), Mathf.Clamp(vec.y, min, max), Mathf.Clamp(vec.z, min, max));
    }
}