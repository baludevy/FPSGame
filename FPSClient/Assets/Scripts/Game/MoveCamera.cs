// MoveCamera

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

    private void Start() {
        Instance = this;
    }

    private void Update() {
        if (smooth) {
            transform.position = Vector3.Lerp(transform.position,
                player.position + bobOffset + crouchOffset + offset, NetworkSettings.tickTime * 5);
        }
        else {
            transform.position = player.position + bobOffset + crouchOffset + offset;
        }
    }

    private void LateUpdate() {
        UpdateBob();

        Vector3 cameraRot = playerMovement.cameraRot;
        cameraRot.x = Mathf.Clamp(cameraRot.x, -90f, 90f);
        transform.rotation = Quaternion.Euler(cameraRot);
        Vector3 eulerAngles = transform.rotation.eulerAngles;

        transform.rotation = Quaternion.Euler(eulerAngles);
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