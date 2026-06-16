using UnityEngine;

public class WeaponSway : MonoBehaviour {
    public static WeaponSway Instance;

    public float smooth = 10f;
    public float swayMult = 2f;

    public float bobMultiplier = 1f;
    public float bobSpeed = 15f;

    public float moveBobX = 0.04f;
    public float moveBobY = 0.03f;
    public float moveBobFrequency = 10f;
    public float moveBobSpeedThreshold = 0.1f;
    public float fallOffsetMultiplier = 0.003f;
    public float fallOffsetMax = 0.3f;

    public bool breathing = true;
    public float breathingSpeed = 1.2f;
    public float breathingPosX = 0.01f;
    public float breathingPosY = 0.015f;
    public float breathingRotX = 0.5f;
    public float breathingRotZ = 0.25f;

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

        Vector3 targetPos = initialPosition + bobOffset + GetBreathPos() + GetMoveBob();

        transform.localPosition = Vector3.Lerp(
            transform.localPosition,
            targetPos,
            smooth * Time.deltaTime
        );

        transform.localRotation = Quaternion.Slerp(
            transform.localRotation,
            targetRot * GetBreathRot(),
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
        if (PlayerMovement.Instance == null) return Vector3.zero;

        PlayerMovement movement = PlayerMovement.Instance;

        float fallOffset = Mathf.Clamp(-movement.fallSpeed * fallOffsetMultiplier, 0f, fallOffsetMax);

        bool canBob = movement.grounded || movement.wallRunning;
        if (!canBob) {
            moveBobTimer = 0f;
            return new Vector3(0f, fallOffset, 0f);
        }

        Rigidbody rb = movement.rb;
        float horizontalSpeed = new Vector3(rb.velocity.x, 0f, rb.velocity.z).magnitude;

        if (horizontalSpeed < moveBobSpeedThreshold) {
            moveBobTimer = 0f;
            return new Vector3(0f, fallOffset, 0f);
        }

        float speedFactor = Mathf.Clamp01(horizontalSpeed / movement.runSpeed);
        moveBobTimer += Time.deltaTime * moveBobFrequency * speedFactor;

        return new Vector3(
            Mathf.Cos(moveBobTimer * 0.5f) * moveBobX * speedFactor,
            Mathf.Abs(Mathf.Sin(moveBobTimer)) * moveBobY * speedFactor + fallOffset,
            0f
        );
    }

    public void BobOnce(Vector3 direction) {
        desiredBob += direction * bobMultiplier;
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
    }
}