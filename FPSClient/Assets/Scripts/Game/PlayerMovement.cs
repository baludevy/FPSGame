using System;
using EZCameraShake;
using UnityEngine;

// ReSharper disable All

public class PlayerMovement : MonoBehaviour {
    //Assignables
    public Transform playerCam;
    public Transform orientation;
    public Rigidbody rb;
    public ParticleSystem ps;

    public TickInvoker tickInvoker = new();

    //Rotation and look
    private float xRotation;
    private float sensitivity = 50f;
    private float sensMultiplier = 1f;

    //movement
    public float moveSpeed = 4000f;
    public float runSpeed = 12f;
    public bool grounded, wasGrounded, cancellingGrounded;
    public LayerMask whatIsGround;

    public float counterMovement = 0.175f;
    private float threshold = 0.01f;
    public float maxSlopeAngle = 35f;

    //crouch
    public float slideForce = 400;
    public float slideCounterMovement = 0.01f;

    //jumping
    public bool readyToJump = true;
    public float jumpForce = 9f;

    //wallrunning
    public bool wallRunning;
    private bool surfing;
    private float wallRunRotation;
    private bool readyToWallrun = true;
    private float actualWallRotation;
    private float wallRotationVel;
    public bool wallrunBoostUsed;
    public int wallRunTicks;
    private bool pressingTowardWall;
    private int wallAttachTicks;
    private int lastWallInstanceId = 0;
    private bool sameWallOnCooldown = false;
    private Vector3 initialWallNormal;
    private float lockedWallSign;
    private int currentFacingWallId = 0;

    //input
    private float x, y;
    private bool jumping, sprinting, crouching;
    public Vector3 cameraRot;
    public float desiredX;

    //sliding
    private bool isCrouching;
    private Vector3 normalVector = Vector3.up;
    private Vector3 wallNormalVector;
    private float playerHeight;
    private float slideSlowdown;

    //other
    private ParticleSystem.EmissionModule psEmission;
    private float fallSpeed;
    private Vector3 lastMoveSpeed;
    private CapsuleCollider playerCollider;

    public static PlayerMovement Instance { get; private set; }

    private void Awake() {
        Instance = this;
        rb = GetComponent<Rigidbody>();
        playerCollider = GetComponent<CapsuleCollider>();
        playerHeight = playerCollider.bounds.size.y;
        psEmission = ps.emission;
    }

    private void Start() {
        readyToJump = true;
        wallNormalVector = Vector3.up;
        CursorManager.DisableCursor();
        CameraShake();
    }

    private void Update() {
        fallSpeed = rb.velocity.y;
        lastMoveSpeed = VectorExtensions.XZVector(rb.velocity);
        Look();
    }

    public void AdvanceLogic() {
        tickInvoker.Step();
        CheckGrounded();
        CheckWalls();
        Movement();
        FindWallRunRotation();
        WallRunning();
        
        Debug.Log(wallRunning);
    }

    public void SetInput(float x, float y, float orientation, bool jumping, bool crouching) {
        if (crouching && !this.crouching) StartCrouch();
        else if (!crouching && this.crouching) StopCrouch();

        this.x = x;
        this.y = y;
        this.jumping = jumping;
        this.orientation.localRotation = Quaternion.Euler(0f, orientation, 0f);
        this.crouching = crouching;
        this.isCrouching = crouching;
    }

    public void Movement() {
        rb.AddForce(Vector3.down * NetworkSettings.tickTime * 12.5f);

        Vector2 mag = FindVelRelativeToLook();

        CounterMovement(x, y, mag);

        if (readyToJump && jumping) Jump();

        if (crouching && grounded && readyToJump) {
            rb.AddForce(Vector3.down * NetworkSettings.tickTime * 3000f);
        }

        float inputX = x;
        if ((x > 0 && mag.x > runSpeed) || (x < 0 && mag.x < -runSpeed)) {
            inputX = 0;
        }

        float inputY = y;
        if ((y > 0 && mag.y > runSpeed) || (y < 0 && mag.y < -runSpeed)) {
            inputY = 0;
        }

        float multX = 1f;
        float multY = 1f;

        if (!grounded) {
            multX = 0.6f;
            multY = 0.6f;
        }

        if (grounded && crouching) {
            multX = 0f;
            multY = 0f;
        }

        if (wallRunning) {
            multX = 0.3f;
            multY = 0.3f;
        }

        if (surfing) {
            multX = 0.7f;
            multY = 0.3f;
        }

        rb.AddForce(orientation.forward * (inputY * moveSpeed * NetworkSettings.tickTime * multX * multY));
        rb.AddForce(orientation.right * (inputX * moveSpeed * NetworkSettings.tickTime * multX));

        SpeedLines();
    }

    private void StartCrouch() {
        transform.localScale = new Vector3(1f, 0.5f, 1f);
        transform.localPosition = new Vector3(transform.position.x, transform.position.y - 1f, transform.position.z);

        playerHeight = 1f;

        if (grounded && rb.velocity.magnitude > 2f) {
            rb.AddForce(orientation.forward * slideForce, ForceMode.Impulse);
        }
    }

    private void StopCrouch() {
        transform.localScale = new Vector3(1f, 1.5f, 1f);
        transform.localPosition = new Vector3(transform.position.x, transform.position.y + 1f, transform.position.z);

        playerHeight = 3f;
    }

    private void SpeedLines() {
        float viewAngleFactor = Mathf.Max(1f, Vector3.Angle(rb.velocity, playerCam.forward) * 0.5f);

        if (grounded && !wallRunning) {
            psEmission.rateOverTimeMultiplier = 0f;
        }
        else {
            psEmission.rateOverTimeMultiplier = rb.velocity.magnitude / viewAngleFactor;
        }
    }

    public void CheckGrounded() {
        wasGrounded = grounded;
        grounded = Physics.SphereCast(transform.position, 0.45f, Vector3.down, out RaycastHit hit,
            (playerHeight / 2f) - 0.35f, whatIsGround);

        if (grounded) {
            normalVector = hit.normal;
            wallrunBoostUsed = false;
            surfing = IsSurf(hit.normal);
            if (sameWallOnCooldown) {
                sameWallOnCooldown = false;
                lastWallInstanceId = 0;
            }
        }
        else {
            normalVector = Vector3.up;
            surfing = false;
        }

        if (!wasGrounded && grounded && MoveCamera.Instance != null) {
            MoveCamera.Instance.BobOnce(new Vector3(0f, fallSpeed, 0f));
        }
    }

    private bool TryFindWall(Vector3 direction, out RaycastHit hit) {
        if (Physics.Raycast(transform.position, direction, out hit, 1f, whatIsGround) && IsWall(hit.normal)) {
            return true;
        }

        return false;
    }

    private int GetWallIdentifier(RaycastHit hit) {
        int colliderId = hit.collider.GetInstanceID();
        Vector3 n = hit.normal;
        int nx = Mathf.RoundToInt(n.x * 10f);
        int ny = Mathf.RoundToInt(n.y * 10f);
        int nz = Mathf.RoundToInt(n.z * 10f);
        int planeOffset = Mathf.RoundToInt(Vector3.Dot(hit.point, n) * 2f);
        int hash = colliderId;
        hash = hash * 397 ^ nx;
        hash = hash * 397 ^ ny;
        hash = hash * 397 ^ nz;
        hash = hash * 397 ^ planeOffset;
        return hash;
    }

    public void CheckWalls() {
        RaycastHit hit = default;
        bool foundWall = false;
        pressingTowardWall = false;

        if (!wallRunning) {
            if (x < -0.1f && TryFindWall(-orientation.right, out hit)) {
                foundWall = true;
                lockedWallSign = -1f;
                initialWallNormal = hit.normal;
            }
            else if (x > 0.1f && TryFindWall(orientation.right, out hit)) {
                foundWall = true;
                lockedWallSign = 1f;
                initialWallNormal = hit.normal;
            }
            else if (y < -0.1f && TryFindWall(-orientation.forward, out hit)) {
                foundWall = true;
                lockedWallSign = 0f;
                initialWallNormal = hit.normal;
            }
            else if (y > 0.1f && TryFindWall(orientation.forward, out hit)) {
                foundWall = true;
                lockedWallSign = 0f;
                initialWallNormal = hit.normal;
            }
        }
        else {
            if (Physics.Raycast(transform.position, -initialWallNormal, out hit, 1.2f, whatIsGround) &&
                IsWall(hit.normal)) {
                foundWall = true;

                if (lockedWallSign == 1f) pressingTowardWall = x > -0.1f;
                else if (lockedWallSign == -1f) pressingTowardWall = x < 0.1f;
                else pressingTowardWall = true;
            }
        }

        if (!foundWall) {
            wallRunning = false;
            currentFacingWallId = 0;
            return;
        }

        wallNormalVector = hit.normal;
        currentFacingWallId = GetWallIdentifier(hit);

        if (!grounded && readyToWallrun) {
            if (sameWallOnCooldown && currentFacingWallId == lastWallInstanceId) {
                wallRunning = false;
                return;
            }

            if (!wallRunning) {
                Vector3 wallForward = Vector3.Cross(wallNormalVector, Vector3.up).normalized;
                float forwardVel = Vector3.Dot(rb.velocity, wallForward);

                rb.velocity -= wallForward * (forwardVel * 0.4f);
                rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);

                if (!wallrunBoostUsed) {
                    rb.AddForce(Vector3.up * 14f, ForceMode.Impulse);
                    wallrunBoostUsed = true;
                }

                wallRunTicks = 0;
                wallAttachTicks = 0;
            }

            wallRunning = true;
            wallAttachTicks++;
        }
        else {
            wallRunning = false;
        }

        wallRunTicks++;
        if (wallRunTicks == 160) {
            rb.AddForce(wallNormalVector * 1200f);
            wallRunning = false;
            readyToWallrun = true;
            wallRunTicks = 0;
        }
    }

    private void CameraShake() {
        float shakeStrength = rb.velocity.magnitude / 10f;
        CameraShaker.Instance.ShakeOnce(shakeStrength, 0.025f * shakeStrength, 0.25f, 0.2f);
        Invoke(nameof(CameraShake), 0.2f);
    }

    private void Jump() {
        bool canJump = grounded || wallRunning || surfing;
        if (!canJump || !readyToJump) return;

        bool isWallJump = wallRunning;
        if (isWallJump) {
            if (wallAttachTicks < 10) return;

            lastWallInstanceId = currentFacingWallId;

            Vector3 pushNormal = wallNormalVector;

            sameWallOnCooldown = true;
            tickInvoker.Invoke(ResetSameWallCooldown, 64);
            readyToJump = false;

            rb.velocity = new Vector3(rb.velocity.x, 0, rb.velocity.z);
            rb.AddForce(pushNormal * 16f + Vector3.up * 10f, ForceMode.Impulse);

            wallRunning = false;
            surfing = false;
            wallrunBoostUsed = false;
            currentFacingWallId = 0;

            tickInvoker.Invoke(ResetJump, 10);
            return;
        }

        rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        rb.AddForce(Vector3.up * jumpForce * 1.5f, ForceMode.Impulse);
    }

    public void Look() {
        float mouseX = Input.GetAxis("Mouse X") * sensitivity * Time.fixedDeltaTime * sensMultiplier;
        float mouseY = Input.GetAxis("Mouse Y") * sensitivity * Time.fixedDeltaTime * sensMultiplier;

        desiredX = playerCam.localRotation.eulerAngles.y + mouseX;
        xRotation = Mathf.Clamp(xRotation - mouseY, -90f, 90f);

        actualWallRotation = Mathf.SmoothDamp(actualWallRotation, wallRunRotation, ref wallRotationVel, 0.3f);

        cameraRot = new Vector3(xRotation, desiredX, actualWallRotation);
        playerCam.localRotation = Quaternion.Euler(cameraRot);
    }

    private void CounterMovement(float x, float y, Vector2 mag) {
        if (!grounded || jumping || wallRunning) return;

        Vector3 vel = new Vector3(rb.velocity.x, 0f, rb.velocity.z);

        float currentCounterMultiplier = counterMovement;
        if (crouching) {
            rb.AddForce(
                -vel * moveSpeed * slideCounterMovement * NetworkSettings.tickTime);

            return;
        }

        if (Math.Abs(mag.x) > threshold && Math.Abs(x) < 0.05f || (mag.x < -threshold && x > 0) ||
            (mag.x > threshold && x < 0)) {
            rb.AddForce(moveSpeed * orientation.right * NetworkSettings.tickTime * -mag.x * currentCounterMultiplier);
        }

        if (Math.Abs(mag.y) > threshold && Math.Abs(y) < 0.05f || (mag.y < -threshold && y > 0) ||
            (mag.y > threshold && y < 0)) {
            rb.AddForce(moveSpeed * orientation.forward * NetworkSettings.tickTime * -mag.y * currentCounterMultiplier);
        }

        if (crouching) return;

        if (vel.magnitude > runSpeed) {
            Vector3 limitedVel = vel.normalized * runSpeed;
            rb.velocity = new Vector3(limitedVel.x, rb.velocity.y, limitedVel.z);
        }
    }

    public Vector2 FindVelRelativeToLook() {
        float lookAngle = orientation.eulerAngles.y;
        float moveAngle = Mathf.Atan2(rb.velocity.x, rb.velocity.z) * Mathf.Rad2Deg;
        float u = Mathf.DeltaAngle(lookAngle, moveAngle);
        float v = 90 - u;
        float magnitude = new Vector2(rb.velocity.x, rb.velocity.z).magnitude;
        return new Vector2(magnitude * Mathf.Cos(v * Mathf.Deg2Rad), magnitude * Mathf.Cos(u * Mathf.Deg2Rad));
    }

    private void FindWallRunRotation() {
        if (!wallRunning || lockedWallSign == 0f) {
            wallRunRotation = 0f;
            return;
        }

        float cameraAngle = playerCam.rotation.eulerAngles.y;
        float wallAngle = Vector3.SignedAngle(Vector3.forward, wallNormalVector, Vector3.up);
        wallRunRotation = (-Mathf.DeltaAngle(cameraAngle, wallAngle) / 90f) * 5.5f;
    }

    public void WallRunning() {
        if (!wallRunning) return;

        rb.AddForce(-wallNormalVector * NetworkSettings.tickTime * moveSpeed * 2.5f);

        Vector3 wallForward = Vector3.Cross(wallNormalVector, Vector3.up);
        if (Vector3.Dot(wallForward, orientation.forward) < 0f) wallForward = -wallForward;

        Vector3 travelForce = wallForward * 150f;
        if (y > 0.1f) {
            travelForce = wallForward * 1000f; //forwards
        }
        else if (y < -0.1f) {
            travelForce = -wallForward * 600f; //backwards
        }

        rb.AddForce(travelForce * NetworkSettings.tickTime);

        float gravityMult = -1f;
        if (isCrouching) {
            gravityMult = -2f;
        }

        if (!jumping && pressingTowardWall && rb.velocity.y < gravityMult) {
            rb.velocity = new Vector3(rb.velocity.x, Mathf.Max(rb.velocity.y, gravityMult), rb.velocity.z);
        }
    }

    private bool IsSurf(Vector3 v) {
        float angle = Vector3.Angle(Vector3.up, v);
        return angle < 89f && angle > maxSlopeAngle;
    }

    private bool IsWall(Vector3 v) {
        return Math.Abs(90f - Vector3.Angle(Vector3.up, v)) < 0.1f;
    }

    private void ResetJump() {
        readyToJump = true;
    }

    private void ResetSameWallCooldown() {
        sameWallOnCooldown = false;
        lastWallInstanceId = 0;
    }
}