using UnityEngine;

public class MoveWeapon : MonoBehaviour {
    public static MoveWeapon Instance;

    [Header("Rotational sway")]
    public float swayMult = 0.5f;
    public float stiffness = 400f;
    public float damping = 28f;
    public float maxSway = 0.15f;

    [Header("Positional sway")]
    public float posSwayMult = 0.15f;
    public float posStiffness = 300f;
    public float posDamping = 22f;
    public float maxPosSway = 0.03f;

    [Header("Tilt")]
    public float tiltMult = 1.2f;
    public float maxTilt = 4f;

    [Header("Fall inertia")]
    public float fallInertiaMult = 0.01f;
    public float fallInertiaStiffness = 200f;
    public float fallInertiaDamping = 18f;
    public float maxFallInertia = 0.06f;

    [Header("Bob")]
    public float bobSpeed = 3f;
    public float bobRefSpeed = 5.5f;
    public float bobXAmount = 0.008f;
    public float bobYAmount = 0.005f;
    public float bobRollAmount = 0.5f;
    public float bobWeightSpeed = 4f;
    public float bobSnapSpeed = 15f;

    [Header("Bob kick (land or jump)")]
    public float bobKickStiffness = 250f;
    public float bobKickDamping = 18f;

    private Vector3 currentRot;
    private Vector3 rotVelocity;
    private Vector3 currentPos;
    private Vector3 posVelocity;

    private Vector3 fallOffset;
    private Vector3 fallVelocity;

    private Vector3 bobPos;
    private Vector3 bobRot;
    private float bobCycle;
    private float currentBobWeight;

    private Vector3 kickPos;
    private Vector3 kickPosVelocity;
    private Vector3 kickRot;
    private Vector3 kickRotVelocity;

    private Quaternion baseRotation;
    private Vector3 basePosition;

    private void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start() {
        baseRotation = transform.localRotation;
        basePosition = transform.localPosition;
    }

    private void LateUpdate() {
        float dt = Time.deltaTime;

        // mouse sway
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        Vector3 rotTarget = new Vector3(
            -mouseY * swayMult,
             mouseX * swayMult,
            -mouseX * tiltMult
        );
        rotTarget.x = Mathf.Clamp(rotTarget.x, -maxSway, maxSway);
        rotTarget.y = Mathf.Clamp(rotTarget.y, -maxSway, maxSway);
        rotTarget.z = Mathf.Clamp(rotTarget.z, -maxTilt, maxTilt);

        Vector3 rotForce = (rotTarget - currentRot) * stiffness - rotVelocity * damping;
        rotVelocity += rotForce * dt;
        currentRot += rotVelocity * dt;

        Vector3 posTarget = new Vector3(-mouseX, -mouseY, 0f) * posSwayMult;
        posTarget = Vector3.ClampMagnitude(posTarget, maxPosSway);

        Vector3 posForce = (posTarget - currentPos) * posStiffness - posVelocity * posDamping;
        posVelocity += posForce * dt;
        currentPos += posVelocity * dt;

        // fall inertia
        Rigidbody rb = LocalPlayer.Instance.movement.GetRb();
        float fallSpeed = rb.velocity.y;

        Vector3 fallTarget = new Vector3(0f, fallSpeed, 0f) * fallInertiaMult;
        fallTarget = Vector3.ClampMagnitude(fallTarget, maxFallInertia);

        Vector3 fallForce = (fallTarget - fallOffset) * fallInertiaStiffness - fallVelocity * fallInertiaDamping;
        fallVelocity += fallForce * dt;
        fallOffset += fallVelocity * dt;

        // bob
        Vector3 localVel = transform.parent.InverseTransformDirection(rb.velocity);
        float horizontalSpeed = new Vector3(localVel.x, 0f, localVel.z).magnitude;

        bool isGrounded = LocalPlayer.Instance.movement.IsGrounded();
        bool isWallRunning = LocalPlayer.Instance.movement.IsWallRunning();
        bool isSliding = LocalPlayer.Instance.movement.IsSliding();

        float targetBobWeight = 0f;

        if (isGrounded && !isWallRunning && !isSliding) {
            targetBobWeight = Mathf.Clamp01(horizontalSpeed / bobRefSpeed);
        }

        if (targetBobWeight > 0.05f) {
            currentBobWeight = Mathf.MoveTowards(currentBobWeight, targetBobWeight, dt * bobWeightSpeed);
            bobCycle += dt * bobSpeed * (horizontalSpeed / bobRefSpeed);
        }
        else {
            currentBobWeight = Mathf.MoveTowards(currentBobWeight, 0f, dt * bobSnapSpeed);
            bobCycle = Mathf.MoveTowards(bobCycle, Mathf.Round(bobCycle / Mathf.PI) * Mathf.PI, dt * bobSnapSpeed);
        }

        float bobOffsetX = Mathf.Sin(bobCycle) * bobXAmount * currentBobWeight;
        float bobOffsetY = Mathf.Sin(bobCycle * 2f) * bobYAmount * currentBobWeight;
        float bobRoll = Mathf.Sin(bobCycle) * bobRollAmount * currentBobWeight;

        bobPos = new Vector3(bobOffsetX, bobOffsetY, 0f);
        bobRot = new Vector3(0f, 0f, bobRoll);

        // kick springs (driven by BobPos/BobRot calls)
        Vector3 kickPosForce = -kickPos * bobKickStiffness - kickPosVelocity * bobKickDamping;
        kickPosVelocity += kickPosForce * dt;
        kickPos += kickPosVelocity * dt;

        Vector3 kickRotForce = -kickRot * bobKickStiffness - kickRotVelocity * bobKickDamping;
        kickRotVelocity += kickRotForce * dt;
        kickRot += kickRotVelocity * dt;

        // final
        transform.localPosition = basePosition + currentPos + fallOffset + bobPos + kickPos;

        transform.localRotation = baseRotation * Quaternion.Euler(
            currentRot.x + bobRot.x + kickRot.x,
            currentRot.y + bobRot.y + kickRot.y,
            currentRot.z + bobRot.z + kickRot.z
        );
    }
    
    public void BobPos(Vector3 kick) {
        kickPosVelocity += kick;
    }
    
    public void BobRot(Vector3 kick) {
        kickRotVelocity += kick;
    }
}