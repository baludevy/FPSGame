using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCamera : MonoBehaviour {
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private Camera cam;
    [SerializeField] private Transform player;

    [Header("Look")] [SerializeField] private float sensitivity = 0.05f;
    private float maxLookAngle = 90f;

    private float xRotation;
    private float desiredX;
    private Vector3 cameraRot;

    [Header("")] [SerializeField] private Vector3 offset;
    private Vector3 bobOffset;
    [HideInInspector] public float wallRotation;

    [Header("Position bobbing")] [SerializeField]
    private float bobSpeed = 15f;

    [SerializeField] private float bobMultiplier = 0.5f;
    private Vector3 desiredBob;

    [Header("Rotation bobbing")] [SerializeField]
    private float bobRotSpeed = 12f;

    [SerializeField] private float bobRotMultiplier = 1f;
    private Vector3 desiredBobRot;
    private Vector3 bobRotOffset;

    [Header("Fov")] [SerializeField] private float fovSmoothTime = 0.12f;
    [SerializeField] private float maxFovOffset = 15f;
    private float fovVelocity;
    
    [SerializeField] private InputActionReference lookAction; 

    private float baseFov;
    private float targetFov;

    private float baseYaw;
    private float basePitch;
    private float baseRoll;

    private void Start() {
        if (cam != null) baseFov = cam.fieldOfView;
    }

    private void Update() {
        Look();
    }

    private void LateUpdate() {
        transform.position = player.position + bobOffset + offset;

        UpdateFov();
        UpdateBob();
        UpdateBobRot();
    }

    public void Look() {
        Vector2 look = lookAction.action.ReadValue<Vector2>() * sensitivity;
        
        float mouseX = look.x;
        float mouseY = look.y;

        desiredX += mouseX;
        xRotation = Mathf.Clamp(xRotation - mouseY, -maxLookAngle, maxLookAngle);

        SetRotation(desiredX, xRotation, wallRotation);
    }

    private void UpdateFov() {
        if (baseFov <= 0) {
            baseFov = cam.fieldOfView;
            return;
        }

        float fovOffset = 0;

        if (playerMovement.IsSliding()) {
            float speedT = Mathf.InverseLerp(0f, playerMovement.maxSlideSpeed, playerMovement.GetRb().velocity.magnitude);
            fovOffset = Mathf.Lerp(0f, maxFovOffset, speedT);
        }

        targetFov = baseFov + fovOffset;
        targetFov = Mathf.Clamp(targetFov, baseFov, baseFov + maxFovOffset);

        cam.fieldOfView = Mathf.SmoothDamp(cam.fieldOfView, targetFov, ref fovVelocity, fovSmoothTime);
    }

    public void BobOnce(Vector3 bobDirection) {
        Vector3 vector = ClampVector(bobDirection * 0.15f, -3f, 3f);
        desiredBob = vector * bobMultiplier;
    }

    public void BobRotOnce(Vector3 bobDirection) {
        Vector3 rot = ClampVector(bobDirection * 2f, -5f, 5f);
        desiredBobRot = rot * bobRotMultiplier;
    }

    private void UpdateBob() {
        desiredBob = Vector3.Lerp(desiredBob, Vector3.zero, Time.deltaTime * bobSpeed);
        bobOffset = Vector3.Lerp(bobOffset, desiredBob, Time.deltaTime * bobSpeed);
    }

    private void UpdateBobRot() {
        desiredBobRot = Vector3.Lerp(desiredBobRot, Vector3.zero, Time.deltaTime * bobRotSpeed);
        bobRotOffset = Vector3.Lerp(bobRotOffset, desiredBobRot, Time.deltaTime * bobRotSpeed);

        ApplyFinalRotation();
    }

    public void SetRotation(float yaw, float pitch, float roll) {
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

    public Vector3 GetCameraRot() {
        return new Vector3(cameraRot.x, desiredX, wallRotation);
    }

    private Vector3 ClampVector(Vector3 vec, float min, float max) {
        return new Vector3(Mathf.Clamp(vec.x, min, max), Mathf.Clamp(vec.y, min, max), Mathf.Clamp(vec.z, min, max));
    }
    
    private void OnEnable() {
        lookAction.action.Enable();
    }

    private void OnDisable() {
        lookAction.action.Disable();
    }
}