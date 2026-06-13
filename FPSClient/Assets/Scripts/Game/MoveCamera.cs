using UnityEngine;

public class MoveCamera : MonoBehaviour {
    public Transform player;
    public Vector3 offset;
    public static MoveCamera Instance { get; private set; }
    public bool smooth;
    public PlayerMovement playerMovement;
    private float desiredTilt;
    private float tilt;
    private Vector3 desiredBob;
    private Vector3 bobOffset;
    private float bobSpeed = 15f;
    private float bobMultiplier = 0.5f;
    public Vector3 desyncOffset;
    public Vector3 crouchOffset;

    private float strafeRotation;
    private float desiredStrafeRotation;
    private float strafeRotationVel;
    private float strafeTiltAmount = 1.5f;
    private float strafeSmoothTime = 0.3f;

    public Camera cam;
    private float baseFov = 90f;
    private float targetFov;
    private float fovVelocity;
    private float fovSmoothTime = 0.2f;
    private float maxFovOffset = 10f;
    private float speedThreshold = 15f;
    private float maxSpeedForFov = 35f;

    private void Awake() {
        Instance = this;
        if (cam == null) cam = GetComponent<Camera>();
    }

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
    }

    private void LateUpdate() {
        UpdateBob();
        UpdateStrafeRotation();

        Vector3 cameraRot = playerMovement.cameraRot;
        cameraRot.x = Mathf.Clamp(cameraRot.x, -90f, 90f);
        cameraRot.z += strafeRotation;
        transform.rotation = Quaternion.Euler(cameraRot);
        Vector3 eulerAngles = transform.rotation.eulerAngles;
        transform.rotation = Quaternion.Euler(eulerAngles);
    }

    private void UpdateStrafeRotation() {
        float x = Input.GetAxisRaw("Horizontal");
        if (x < -0.1f) {
            desiredStrafeRotation = strafeTiltAmount;
        }
        else if (x > 0.1f) {
            desiredStrafeRotation = -strafeTiltAmount;
        }
        else {
            desiredStrafeRotation = 0f;
        }

        strafeRotation =
            Mathf.SmoothDamp(strafeRotation, desiredStrafeRotation, ref strafeRotationVel, strafeSmoothTime);
    }

    private void UpdateFov() {
        if (cam == null || playerMovement == null || playerMovement.rb == null) return;

        if (baseFov <= 0) {
            baseFov = cam.fieldOfView;
            return;
        }

        float currentSpeed = playerMovement.rb.velocity.magnitude;

        float speedFactor = Mathf.InverseLerp(speedThreshold, maxSpeedForFov, currentSpeed);
        float speedFovOffset = speedFactor * maxFovOffset;

        bool isWallRunning = playerMovement.wallRunning;

        float wallrunFovOffset = isWallRunning ? maxFovOffset : 0f;

        targetFov = baseFov + Mathf.Max(speedFovOffset, wallrunFovOffset);
        targetFov = Mathf.Clamp(targetFov, baseFov, baseFov + maxFovOffset);

        cam.fieldOfView = Mathf.SmoothDamp(cam.fieldOfView, targetFov, ref fovVelocity, fovSmoothTime);
    }

    public void BobOnce(Vector3 bobDirection) {
        Vector3 vector = ClampVector(bobDirection * 0.15f, -3f, 3f);
        desiredBob = vector * bobMultiplier;
    }

    private void UpdateBob() {
        desiredBob = Vector3.Lerp(desiredBob, Vector3.zero, Time.deltaTime * bobSpeed * 0.5f);
        bobOffset = Vector3.Lerp(bobOffset, desiredBob, Time.deltaTime * bobSpeed);
    }

    private Vector3 ClampVector(Vector3 vec, float min, float max) {
        return new Vector3(Mathf.Clamp(vec.x, min, max), Mathf.Clamp(vec.y, min, max), Mathf.Clamp(vec.z, min, max));
    }
}