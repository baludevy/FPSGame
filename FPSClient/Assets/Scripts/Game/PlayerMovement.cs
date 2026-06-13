using System;
using EZCameraShake;
using UnityEngine;

// ReSharper disable All

public class PlayerMovement : MonoBehaviour {
    // Assignables
    public Transform playerCam;
    public Transform orientation;
    public Rigidbody rb;
    public ParticleSystem ps;

    public TickInvoker tickInvoker = new();

    // Rotation and look
    private float xRotation;
    private float sensitivity = 50f;
    private float sensMultiplier = 1f;

    // Movement
    public float moveSpeed = 4000f;
    public float runSpeed = 13f;
    public bool grounded, wasGrounded, cancellingGrounded;
    public LayerMask whatIsGround;

    public float counterMovement = 0.175f;
    private float threshold = 0.01f;
    public float maxSlopeAngle = 35f;

    // Crouch & Slide
    private Vector3 slideDirection;
    public float slideForce = 400;
    public float slideCounterMovement = 0.01f;

    // Jumping
    private bool readyToJump = true;
    public float jumpForce = 400f;

    // Wallrunning & Surfing
    private bool wallRunning, surfing;
    private float wallRunRotation;
    private bool readyToWallrun = true;
    private float actualWallRotation;
    private float wallRotationVel;
    private int wallRunTicks;
    private bool pressingTowardWall;
    private int wallAttachTicks;

    // Input
    private float x, y;
    private bool jumping, sprinting, crouching;
    public Vector3 cameraRot;
    private float desiredX;

    // Sliding / States
    private bool isCrouching;
    private Vector3 normalVector = Vector3.up;
    private Vector3 wallNormalVector;
    private float playerHeight;
    private float slideSlowdown;

    private ParticleSystem.EmissionModule psEmission;
    private float fallSpeed;
    private Vector3 lastMoveSpeed;
    private CapsuleCollider playerCollider;
    public static PlayerMovement Instance { get; private set; }

    private Vector3 lastWallNormal = Vector3.zero;
    private bool sameWallOnCooldown = false;

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
    }

    public void SetInput(float x, float y, bool jumping, bool crouching) {
        if (crouching && !this.crouching) StartCrouch();
        else if (!crouching && this.crouching) StopCrouch();

        this.x = x;
        this.y = y;
        this.jumping = jumping;
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

        float inputX = (x > 0 && mag.x > runSpeed) || (x < 0 && mag.x < -runSpeed) ? 0 : x;
        float inputY = (y > 0 && mag.y > runSpeed) || (y < 0 && mag.y < -runSpeed) ? 0 : y;

        float multX = 1f;
        float multY = 1f;

        if (!grounded) {
            multX = 0.5f;
            multY = 0.5f;
        }

        if (grounded && crouching) {
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

        if (grounded && crouching) {
            float forwardInput = y > 0 ? y : 1f;
            rb.AddForce(slideDirection * (forwardInput * moveSpeed * NetworkSettings.tickTime * multX * multY));
        }
        else {
            rb.AddForce(orientation.forward * (inputY * moveSpeed * NetworkSettings.tickTime * multX * multY));
            rb.AddForce(orientation.right * (inputX * moveSpeed * NetworkSettings.tickTime * multX));
        }

        SpeedLines();
    }

    private void StartCrouch() {
        transform.localScale = new Vector3(1f, 0.75f, 1f);
        transform.position = new Vector3(transform.position.x, transform.position.y - 1f, transform.position.z);
        playerHeight = 2f;

        Vector3 flatVelocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        if (flatVelocity.magnitude > 1f) {
            slideDirection = flatVelocity.normalized;
        }
        else {
            slideDirection = orientation.forward;
        }

        if (grounded && rb.velocity.magnitude > 5f) {
            rb.AddForce(slideDirection * slideForce, ForceMode.Impulse);
        }
    }

    private void StopCrouch() {
        transform.localScale = new Vector3(1f, 1.75f, 1f);
        transform.position = new Vector3(transform.position.x, transform.position.y + 1f, transform.position.z);
        playerHeight = 4f;
    }

    private void SpeedLines() {
        float viewAngleFactor = Mathf.Max(1f, Vector3.Angle(rb.velocity, playerCam.forward) * 0.5f);
        psEmission.rateOverTimeMultiplier = (grounded && !wallRunning) ? 0f : rb.velocity.magnitude / viewAngleFactor;
    }

    public void CheckGrounded() {
        wasGrounded = grounded;
        grounded = Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, playerHeight * 0.5f + 0.2f,
            whatIsGround);

        if (grounded) {
            normalVector = hit.normal;
            surfing = IsSurf(hit.normal);
            lastWallNormal = Vector3.zero;
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

    public void CheckWalls() {
        RaycastHit hit = default;
        bool foundWall = false;
        pressingTowardWall = false;

        if (x < -0.1f && TryFindWall(-orientation.right, out hit)) {
            foundWall = pressingTowardWall = true;
        }
        else if (x > 0.1f && TryFindWall(orientation.right, out hit)) {
            foundWall = pressingTowardWall = true;
        }
        else if (wallRunning) {
            foundWall = TryFindWall(-orientation.right, out hit) || TryFindWall(orientation.right, out hit) ||
                        TryFindWall(orientation.forward, out hit) || TryFindWall(-orientation.forward, out hit);
        }

        if (!foundWall) {
            wallRunning = false;
            return;
        }

        wallNormalVector = hit.normal;

        if (!grounded && readyToWallrun) {
            if (Vector3.Dot(wallNormalVector, lastWallNormal) > 0.9f) {
                if (sameWallOnCooldown) {
                    wallRunning = false;
                    return;
                }
            }

            if (!wallRunning) {
                Vector3 wallForward = Vector3.Cross(wallNormalVector, Vector3.up).normalized;
                float forwardVel = Vector3.Dot(rb.velocity, wallForward);

                rb.velocity -= wallForward * (forwardVel * 0.4f); // damp velocity when attaching to a wall
                rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
                rb.AddForce(Vector3.up * 16f, ForceMode.Impulse); // upwards force when attaching to a wall
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
        if (wallRunTicks >= 192) {
            // max amount of ticks the player can wallrun for
            rb.AddForce(wallNormalVector * 600f);
            readyToWallrun = true;
        }
    }

    private void CameraShake() {
        float shakeStrength = rb.velocity.magnitude / 10f;
        CameraShaker.Instance.ShakeOnce(shakeStrength, 0.025f * shakeStrength, 0.25f, 0.2f);
        Invoke(nameof(CameraShake), 0.2f);
    }

    private void Jump() {
        if ((grounded || wallRunning || surfing) && readyToJump) {
            Vector3 velocity = rb.velocity;

            if (wallRunning) {
                if (wallAttachTicks < 5) return;

                lastWallNormal = wallNormalVector;
                sameWallOnCooldown = true;
                tickInvoker.Invoke(ResetSameWallCooldown, 30);
                readyToJump = false;

                rb.velocity = new Vector3(rb.velocity.x, 0, rb.velocity.z);

                rb.AddForce(wallNormalVector * 16f + Vector3.up * 10f, ForceMode.Impulse);

                wallRunning = false;
                surfing = false;

                tickInvoker.Invoke(ResetJump, 10);
                return;
            }

            rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
            rb.AddForce(Vector3.up * jumpForce * 1.5f, ForceMode.Impulse);

            MoveCamera.Instance.BobOnce(Vector3.down);
        }
    }

    public void Look() {
        float mouseX = Input.GetAxis("Mouse X") * sensitivity * Time.fixedDeltaTime * sensMultiplier;
        float mouseY = Input.GetAxis("Mouse Y") * sensitivity * Time.fixedDeltaTime * sensMultiplier;

        desiredX = playerCam.localRotation.eulerAngles.y + mouseX;
        xRotation = Mathf.Clamp(xRotation - mouseY, -90f, 90f);

        actualWallRotation =
            Mathf.SmoothDamp(actualWallRotation, wallRunRotation, ref wallRotationVel, 0.3f); // smoothWallRotTime

        cameraRot = new Vector3(xRotation, desiredX, actualWallRotation);
        playerCam.localRotation = Quaternion.Euler(cameraRot);
        orientation.localRotation = Quaternion.Euler(0f, desiredX, 0f);
    }

    private void CounterMovement(float x, float y, Vector2 mag) {
        if (!grounded || jumping || wallRunning) return;

        float currentCounterMultiplier = crouching ? slideCounterMovement : counterMovement;

        if (Math.Abs(mag.x) > threshold && Math.Abs(x) < 0.05f || (mag.x < -threshold && x > 0) ||
            (mag.x > threshold && x < 0)) {
            rb.AddForce(moveSpeed * orientation.right * NetworkSettings.tickTime * -mag.x * currentCounterMultiplier);
        }

        if (Math.Abs(mag.y) > threshold && Math.Abs(y) < 0.05f || (mag.y < -threshold && y > 0) ||
            (mag.y > threshold && y < 0)) {
            rb.AddForce(moveSpeed * orientation.forward * NetworkSettings.tickTime * -mag.y * currentCounterMultiplier);
        }

        if (crouching) return;

        Vector3 flatVel = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        if (flatVel.magnitude > runSpeed) {
            Vector3 limitedVel = flatVel.normalized * runSpeed;
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
        if (!wallRunning) {
            wallRunRotation = 0f;
            return;
        }

        float cameraAngle = playerCam.rotation.eulerAngles.y;
        float wallAngle = Vector3.SignedAngle(Vector3.forward, wallNormalVector, Vector3.up);
        wallRunRotation = (-Mathf.DeltaAngle(cameraAngle, wallAngle) / 90f) * 7.5f;
    }

    public void WallRunning() {
        if (wallRunning) {
            rb.AddForce(-wallNormalVector * NetworkSettings.tickTime * moveSpeed * 2.5f);
            Vector3 wallForward = Vector3.Cross(wallNormalVector, Vector3.up);

            if (Vector3.Dot(wallForward, orientation.forward) < 0f) wallForward = -wallForward;

            Vector3 travelForce = wallForward * 150f; // force when directional input
            if (y > 0.1f) travelForce = wallForward * 1000f; // force when pressing forward
            else if (y < -0.1f) travelForce = -wallForward * 300f; // force when pressing backwards

            rb.AddForce(travelForce * NetworkSettings.tickTime);

            if (!jumping && pressingTowardWall && rb.velocity.y < (isCrouching ? -2f : 0f)) {
                rb.velocity = new Vector3(rb.velocity.x,
                    Mathf.Lerp(rb.velocity.y, isCrouching ? -2f : 0f, NetworkSettings.tickTime * 10f), rb.velocity.z);
            }
        }
    }

    private bool IsSurf(Vector3 v) =>
        Vector3.Angle(Vector3.up, v) < 89f && Vector3.Angle(Vector3.up, v) > maxSlopeAngle;

    private bool IsWall(Vector3 v) => Math.Abs(90f - Vector3.Angle(Vector3.up, v)) < 0.1f;

    private void ResetJump() {
        readyToJump = true;
    }

    private void ResetSameWallCooldown() {
        sameWallOnCooldown = false;
    }
}