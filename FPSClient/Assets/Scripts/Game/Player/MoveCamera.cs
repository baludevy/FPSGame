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

    private Vector3 desiredBobRot;
    private Vector3 bobRotOffset;

    private float bobRotSpeed = 12f;
    private float bobRotMultiplier = 1f;

    private float baseFov = 95f;
    private float targetFov;

    private float fovVelocity;

    private float fovSmoothTime = 0.12f;
    private float maxFovOffset = 10f;

    private float baseYaw;
    private float basePitch;
    private float baseRoll;

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
        UpdateBobRot();
    }

    private void UpdateFov() {
        if (baseFov <= 0) {
            baseFov = cam.fieldOfView;
            return;
        }
        
        float fovOffset = 0;

        if (playerMovement.IsSliding())
            fovOffset = 8;

        targetFov = baseFov + fovOffset;
        targetFov = Mathf.Clamp(targetFov, baseFov, baseFov + maxFovOffset);

        cam.fieldOfView = Mathf.SmoothDamp(cam.fieldOfView, targetFov, ref fovVelocity, fovSmoothTime);
    }

    public void BobOnce(Vector3 bobDirection) {
        Vector3 vector = ClampVector(bobDirection * 0.15f, -3f, 3f);
        desiredBob = vector * bobMultiplier;

        if (MoveWeapon.Instance != null)
            MoveWeapon.Instance.BobOnce(-vector * 0.7f);
    }

    public void BobRotOnce(Vector3 bobDirection) {
        Vector3 rot = ClampVector(bobDirection * 2f, -5f, 5f);
        desiredBobRot = rot * bobRotMultiplier;
    }

    private void UpdateBob() {
        desiredBob = Vector3.Lerp(desiredBob, Vector3.zero, Time.deltaTime * bobSpeed * 0.5f);
        bobOffset = Vector3.Lerp(bobOffset, desiredBob, Time.deltaTime * bobSpeed);
    }

    private void UpdateBobRot() {
        desiredBobRot = Vector3.Lerp(desiredBobRot, Vector3.zero, Time.deltaTime * bobRotSpeed * 0.5f);
        bobRotOffset = Vector3.Lerp(bobRotOffset, desiredBobRot, Time.deltaTime * bobRotSpeed);

        ApplyFinalRotation();
    }

    public void SetLookRotation(float yaw, float pitch, float roll) {
        baseYaw = yaw;
        basePitch = Mathf.Clamp(pitch, -85f, 85f);
        baseRoll = roll;

        ApplyFinalRotation();
    }

    private void ApplyFinalRotation() {
        Quaternion baseRot = Quaternion.Euler(basePitch, baseYaw, baseRoll);
        Quaternion bobRot = Quaternion.Euler(bobRotOffset);

        transform.localRotation = baseRot * bobRot;
    }

    private Vector3 ClampVector(Vector3 vec, float min, float max) {
        return new Vector3(Mathf.Clamp(vec.x, min, max), Mathf.Clamp(vec.y, min, max), Mathf.Clamp(vec.z, min, max));
    }
}