using UnityEngine;

public class MoveWeapon : MonoBehaviour {
    public static MoveWeapon Instance;

    [Header("Rotational sway")] public float swayMult = 0.5f;
    public float stiffness = 400f;
    public float damping = 28f;
    public float maxSway = 0.15f;

    [Header("Positional sway")] public float posSwayMult = 0.15f;
    public float posStiffness = 300f;
    public float posDamping = 22f;
    public float maxPosSway = 0.03f;

    [Header("Tilt")] public float tiltMult = 1.2f;
    public float maxTilt = 4f;

    [Header("Fall inertia")] public float fallInertiaMult = 0.01f;
    public float fallInertiaStiffness = 200f;
    public float fallInertiaDamping = 18f;
    public float maxFallInertia = 0.06f;

    [Header("Bob")] public float bobSpeed = 3f;
    public float bobRefSpeed = 5.5f;
    public float bobXAmount = 0.008f;
    public float bobYAmount = 0.005f;
    public float bobRollAmount = 0.5f;
    public float bobWeightSpeed = 4f;
    public float bobSnapSpeed = 15f;

    [Header("Bob kick (land or jump)")] public float bobKickStiffness = 250f;
    public float bobKickDamping = 18f;

    [Header("Recoil")] public Vector3 recoilKick = new Vector3(0f, 0.02f, -0.04f);
    public Vector3 recoilRotKick = new Vector3(-8f, 0f, 0f);
    public float recoilRotSideScatter = 3f;
    public float recoilStiffness = 220f;
    public float recoilDamping = 16f;

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

    private Vector3 recoilPos;
    private Vector3 recoilPosVelocity;
    private Vector3 recoilRot;
    private Vector3 recoilRotVelocity;

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

        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        Rigidbody rb = LocalPlayer.Instance.movement.GetRb();
        bool isGrounded = LocalPlayer.Instance.movement.IsGrounded();
        bool isWallRunning = LocalPlayer.Instance.movement.IsWallRunning();
        bool isSliding = LocalPlayer.Instance.movement.IsSliding();

        UpdateSway(dt, mouseX, mouseY);
        UpdateFallInertia(dt, rb);
        UpdateBob(dt, rb, isGrounded, isWallRunning, isSliding);
        UpdateKickSprings(dt);
        UpdateRecoilSprings(dt);

        transform.localPosition = basePosition + currentPos + fallOffset + bobPos + kickPos + recoilPos;

        transform.localRotation = baseRotation * Quaternion.Euler(
            currentRot.x + bobRot.x + kickRot.x + recoilRot.x,
            currentRot.y + bobRot.y + kickRot.y + recoilRot.y,
            currentRot.z + bobRot.z + kickRot.z + recoilRot.z
        );
    }

    private void UpdateSway(float dt, float mouseX, float mouseY) {
        Vector3 rotTarget = new Vector3(-mouseY * swayMult, mouseX * swayMult, -mouseX * tiltMult);
        rotTarget.x = Mathf.Clamp(rotTarget.x, -maxSway, maxSway);
        rotTarget.y = Mathf.Clamp(rotTarget.y, -maxSway, maxSway);
        rotTarget.z = Mathf.Clamp(rotTarget.z, -maxTilt, maxTilt);

        SolveSpring(ref currentRot, ref rotVelocity, rotTarget, stiffness, damping, dt);

        Vector3 posTarget = new Vector3(-mouseX, -mouseY, 0f) * posSwayMult;
        posTarget = Vector3.ClampMagnitude(posTarget, maxPosSway);

        SolveSpring(ref currentPos, ref posVelocity, posTarget, posStiffness, posDamping, dt);
    }

    private void UpdateFallInertia(float dt, Rigidbody rb) {
        float fallSpeed = rb.velocity.y;
        Vector3 fallTarget = new Vector3(0f, fallSpeed, 0f) * fallInertiaMult;
        fallTarget = Vector3.ClampMagnitude(fallTarget, maxFallInertia);

        SolveSpring(ref fallOffset, ref fallVelocity, fallTarget, fallInertiaStiffness, fallInertiaDamping, dt);
    }

    private void UpdateBob(float dt, Rigidbody rb, bool isGrounded, bool isWallRunning, bool isSliding) {
        Vector3 localVel = transform.parent.InverseTransformDirection(rb.velocity);
        float horizontalSpeed = new Vector3(localVel.x, 0f, localVel.z).magnitude;

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
    }

    private void UpdateKickSprings(float dt) {
        SolveSpring(ref kickPos, ref kickPosVelocity, Vector3.zero, bobKickStiffness, bobKickDamping, dt);
        SolveSpring(ref kickRot, ref kickRotVelocity, Vector3.zero, bobKickStiffness, bobKickDamping, dt);
    }

    private void UpdateRecoilSprings(float dt) {
        SolveSpring(ref recoilPos, ref recoilPosVelocity, Vector3.zero, recoilStiffness, recoilDamping, dt);
        SolveSpring(ref recoilRot, ref recoilRotVelocity, Vector3.zero, recoilStiffness, recoilDamping, dt);
    }

    private void SolveSpring(ref Vector3 current, ref Vector3 velocity, Vector3 target, float stiffness, float damping,
        float dt) {
        Vector3 displacement = current - target;
        float dampingRatio = damping / (2f * Mathf.Sqrt(stiffness));

        if (dampingRatio >= 1f) {
            float omega = Mathf.Sqrt(stiffness);
            float exp = Mathf.Exp(-omega * dt);
            Vector3 c1 = velocity + displacement * omega;

            current = target + (displacement + c1 * dt) * exp;
            velocity = (velocity - c1 * (omega * dt)) * exp;
        }
        else {
            float omegaN = Mathf.Sqrt(stiffness);
            float omegaD = omegaN * Mathf.Sqrt(1f - dampingRatio * dampingRatio);
            float exp = Mathf.Exp(-dampingRatio * omegaN * dt);

            float cos = Mathf.Cos(omegaD * dt);
            float sin = Mathf.Sin(omegaD * dt);

            Vector3 c1 = displacement;
            Vector3 c2 = (velocity + displacement * (dampingRatio * omegaN)) / omegaD;

            current = target + (c1 * cos + c2 * sin) * exp;
            velocity = ((-c1 * omegaD + c2 * (-dampingRatio * omegaN)) * sin +
                        (c2 * omegaD - c1 * (dampingRatio * omegaN)) * cos) * exp;
        }
    }

    public void BobPos(Vector3 kick) {
        kickPosVelocity += kick;
    }

    public void BobRot(Vector3 kick) {
        kickRotVelocity += kick;
    }

    public void Recoil(float mult = 1f) {
        recoilPos += recoilKick * mult;

        Vector3 rot = recoilRotKick * mult;
        if (recoilRotSideScatter > 0f) {
            float scatter = Random.Range(-recoilRotSideScatter, recoilRotSideScatter) * mult;
            rot.y += scatter;
            rot.z += scatter * 0.5f;
        }

        recoilRot += rot;
    }
}