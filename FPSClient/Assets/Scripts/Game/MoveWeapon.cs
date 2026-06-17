using UnityEngine;

public class MoveWeapon : MonoBehaviour {
    public static MoveWeapon Instance;

    [Header("Weapon Sway")] public float smooth = 10f;
    public float swayMult = 2f;

    [Header("Bob")] public float bobMultiplier = 1f;
    public float bobSpeed = 15f;
    private Vector3 desiredRotBob;
    private Vector3 rotBobOffset;

    [Header("Movement bob")] public float moveBobX = 0.04f;
    public float moveBobY = 0.03f;
    public float moveBobFrequency = 10f;
    public float moveBobSpeedThreshold = 0.1f;
    public float fallOffsetMultiplier = 0.003f;
    public float fallOffsetMax = 0.3f;

    [Header("Breathing")] public bool breathing = true;
    public float breathingSpeed = 1.2f;
    public float breathingPosX = 0.01f;
    public float breathingPosY = 0.015f;
    public float breathingRotX = 0.5f;
    public float breathingRotZ = 0.25f;

    [Header("Recoil")] public Vector3 recoilPosition;
    public Vector3 recoilRotation;

    public float recoilReturnSpeed = 30f;
    public float recoilSnappiness = 100f;

    private Vector3 recoilPosCurrent;
    private Vector3 recoilPosTarget;

    private Vector3 recoilRotCurrent;
    private Vector3 recoilRotTarget;

    [Header("Strafe Tilt")] public float strafeTiltAmount = 0.1f;
    public float strafeTiltSmooth = 30f;

    private float currentStrafeTilt;

    private Vector3 initialPosition;
    private Vector3 desiredBob;
    private Vector3 bobOffset;
    private float moveBobTimer;

    private void Awake() {
        Instance = this;
    }

    private void Start() {
        initialPosition = transform.localPosition;
    }

    private void Update() {
        float x = Input.GetAxisRaw("Mouse X");
        float y = Input.GetAxisRaw("Mouse Y");

        Quaternion rotX = Quaternion.AngleAxis(-y * swayMult, Vector3.right);
        Quaternion rotY = Quaternion.AngleAxis(x * swayMult, Vector3.up);
        Quaternion targetRot = rotX * rotY;

        UpdateBob();

        UpdateRecoil();

        Vector3 targetPos =
            initialPosition +
            bobOffset +
            GetBreathPos() +
            GetMoveBob() +
            recoilPosCurrent;

        transform.localPosition = Vector3.Lerp(
            transform.localPosition,
            targetPos,
            smooth * Time.deltaTime
        );

        transform.localRotation = Quaternion.Slerp(
            transform.localRotation,
            targetRot * GetBreathRot() * Quaternion.Euler(recoilRotCurrent) * Quaternion.Euler(rotBobOffset) *
            Quaternion.Euler(0f, 0f, GetStrafeTilt()),
            smooth * Time.deltaTime
        );
    }

    private Vector3 GetBreathPos() {
        if (!breathing) return Vector3.zero;

        float t = Time.time * breathingSpeed;

        return new Vector3(
            Mathf.Sin(t * 0.8f) * breathingPosX,
            Mathf.Sin(t) * breathingPosY,
            0f
        );
    }

    private Quaternion GetBreathRot() {
        if (!breathing) return Quaternion.identity;

        float t = Time.time * breathingSpeed;

        return Quaternion.Euler(
            Mathf.Sin(t) * breathingRotX,
            0f,
            Mathf.Sin(t * 0.5f) * breathingRotZ
        );
    }

    private Vector3 GetMoveBob() {
        if (LocalPlayer.Instance == null) return Vector3.zero;

        PlayerMovement movement = LocalPlayer.Instance.movement;

        float fallOffset = Mathf.Clamp(-movement.GetFallSpeed() * fallOffsetMultiplier, 0f, fallOffsetMax);

        bool canBob = (movement.IsGrounded() || movement.IsWallRunning()) && !movement.IsCrouching();
        if (!canBob) {
            moveBobTimer = 0f;
            return new Vector3(0f, fallOffset, 0f);
        }

        Rigidbody rb = movement.GetRb();
        float horizontalSpeed = new Vector3(rb.velocity.x, 0f, rb.velocity.z).magnitude;

        if (horizontalSpeed < moveBobSpeedThreshold) {
            moveBobTimer = 0f;
            return new Vector3(0f, fallOffset, 0f);
        }

        moveBobTimer += Time.deltaTime * moveBobFrequency;

        return new Vector3(
            Mathf.Cos(moveBobTimer * 0.5f) * moveBobX,
            Mathf.Abs(Mathf.Sin(moveBobTimer)) * moveBobY + fallOffset,
            0f
        );
    }

    private float GetStrafeTilt() {
        if (LocalPlayer.Instance == null) return 0f;

        Rigidbody rb = LocalPlayer.Instance.movement.GetRb();

        float sidewaysVel = Vector3.Dot(rb.velocity, transform.parent.right);

        float targetTilt = -sidewaysVel * strafeTiltAmount;
        currentStrafeTilt = Mathf.Lerp(currentStrafeTilt, targetTilt, Time.deltaTime * strafeTiltSmooth);

        return currentStrafeTilt;
    }

    public void BobOnce(Vector3 direction) {
        desiredBob += direction * bobMultiplier;
    }

    public void RotBobOnce(Vector3 rotation) {
        desiredRotBob += rotation * bobMultiplier;
    }

    private void UpdateBob() {
        desiredBob = Vector3.Lerp(
            desiredBob,
            Vector3.zero,
            Time.deltaTime * bobSpeed * 0.5f
        );

        bobOffset = Vector3.Lerp(
            bobOffset,
            desiredBob,
            Time.deltaTime * bobSpeed
        );

        desiredRotBob = Vector3.Lerp(desiredRotBob, Vector3.zero, Time.deltaTime * bobSpeed * 0.5f);
        rotBobOffset = Vector3.Lerp(rotBobOffset, desiredRotBob, Time.deltaTime * bobSpeed);
    }

    private void UpdateRecoil() {
        recoilRotTarget = Vector3.Lerp(recoilRotTarget, Vector3.zero, Time.deltaTime * recoilReturnSpeed);
        recoilPosTarget = Vector3.Lerp(recoilPosTarget, Vector3.zero, Time.deltaTime * recoilReturnSpeed);

        recoilRotCurrent = Vector3.Lerp(recoilRotCurrent, recoilRotTarget, Time.deltaTime * recoilSnappiness);
        recoilPosCurrent = Vector3.Lerp(recoilPosCurrent, recoilPosTarget, Time.deltaTime * recoilSnappiness);
    }

    public void AddRecoil() {
        recoilRotTarget += new Vector3(
            recoilRotation.x + Random.Range(-1f, 1f),
            recoilRotation.y + Random.Range(-1f, 1f),
            recoilRotation.z + Random.Range(-1f, 1f)
        );

        recoilPosTarget += recoilPosition;
    }
}