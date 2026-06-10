using System;
using UnityEngine;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

// ReSharper disable All

public class PlayerMovement : MonoBehaviour {
    //Assingables
    public Transform playerCam;
    public Transform orientation;

    //Other
    public Rigidbody rb;
    public ParticleSystem ps;

    public TickInvoker tickInvoker = new();

    // Rotation and look
    private float xRotation;
    private float sensitivity = 50f;
    private float sensMultiplier = 1f;

    //Movement
    public float moveSpeed = 4000f;
    public float runSpeed = 13f;
    public bool grounded, wasGrounded, cancellingGrounded;
    public LayerMask whatIsGround;

    public float counterMovement = 0.175f;
    private float threshold = 0.01f;
    public float maxSlopeAngle = 35f;

    //Crouch & Slide
    private Vector3 slideDirection;
    public float slideForce = 400;
    public float slideCounterMovement = 0.01f;

    //Jumping
    private bool readyToJump = true;
    public float jumpForce = 400f;

    //Wallrunning & Surfing
    private bool wallRunning, surfing;
    private float wallRunRotation;
    private bool readyToWallrun = true;
    private float actualWallRotation;
    private float wallRotationVel;
    private int wallRunTicks;
    private bool pressingTowardWall;
    private int wallAttachTicks;

    //Input
    private float x, y;
    private bool jumping, sprinting, crouching;
    public Vector3 cameraRot;
    private float desiredX;

    //Sliding
    private bool isCrouching;
    private Vector3 normalVector = Vector3.up;
    private Vector3 wallNormalVector;
    private float playerHeight;
    private float slideSlowdown;

    //Other
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
    }

    private void Start() {
        playerCollider = GetComponent<CapsuleCollider>();

        readyToJump = true;
        wallNormalVector = Vector3.up;
    }

    private void Update() {
        fallSpeed = rb.velocity.y;
    }

    public void AdvanceLogic() {
        tickInvoker.Step();
        
        CheckGrounded();
        CheckWalls();
        Movement();
        CheckWallrunCancellation();
        WallRunning();
        
        // Debug.Log($"{crouching} {NetworkManager.tick}");
    }

    public void SetInputs(float x, float y, float orientation, bool jumping, bool crouching) {
        if (crouching && !this.crouching) {
            StartCrouch();
        }
        else if (!crouching && this.crouching) {
            StopCrouch();
        }

        this.x = x;
        this.y = y;
        this.orientation.rotation = Quaternion.Euler(0, orientation, 0);
        this.jumping = jumping;
        this.crouching = crouching;
        this.isCrouching = crouching;
    }

    public void Movement() {
        rb.AddForce(Vector3.down * NetworkSettings.tickTime * 12.5f);
        Vector2 mag = FindVelRelativeToLook();

        CounterMovement(x, y, mag);

        if (readyToJump && jumping) {
            Jump();
        }

        if (crouching && grounded && readyToJump) {
            rb.AddForce(Vector3.down * NetworkSettings.tickTime * 3000f);
            return;
        }

        float inputX = (x > 0 && mag.x > runSpeed) || (x < 0 && mag.x < -runSpeed) ? 0 : x;
        float inputY = (y > 0 && mag.y > runSpeed) || (y < 0 && mag.y < -runSpeed) ? 0 : y;

        float multiplier = 1f;
        float multiplierV = 1f;

        // air movement
        if (!grounded) {
            multiplier = 0.5f;
            multiplierV = 0.5f;
        }

        if (grounded && crouching) {
            multiplierV = 0f;
        }

        if (wallRunning) {
            multiplierV = 0.3f;
            multiplier = 0.3f;
        }

        if (surfing) {
            multiplier = 0.7f;
            multiplierV = 0.3f;
        }

        if (grounded && crouching) {
            float forwardInput = y > 0 ? y : 1f;
            rb.AddForce(slideDirection * (forwardInput * moveSpeed * NetworkSettings.tickTime * multiplier * multiplierV));
        }
        else {
            rb.AddForce(orientation.transform.forward *
                        (inputY * moveSpeed * NetworkSettings.tickTime * multiplier * multiplierV));
            rb.AddForce(orientation.transform.right * (inputX * moveSpeed * NetworkSettings.tickTime * multiplier));
        }
    }
    //Scale player down
    private void StartCrouch() {
        transform.localScale = new Vector3(1f, 0.75f, 1f);
        transform.localPosition = new Vector3(transform.position.x, transform.position.y - 1f,
            transform.position.z);
        
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

    //Scale player to original size
    private void StopCrouch() {
        transform.localScale = new Vector3(1f, 1.75f, 1f);
        transform.localPosition = new Vector3(transform.position.x, transform.position.y + 1f,
            transform.position.z);
        
        playerHeight = 4f;
    }


    public void CheckGrounded() {
        wasGrounded = grounded;

        RaycastHit hit;

        grounded = Physics.Raycast(
            transform.position,
            Vector3.down,
            out hit,
            playerHeight * 0.5f + 0.2f,
            whatIsGround
        );

        if (grounded) {
            normalVector = hit.normal;
            surfing = IsSurf(hit.normal);
            lastWallNormal = Vector3.zero;
        }
        else {
            normalVector = Vector3.up;
            surfing = false;
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
        else if (y > 0.1f && TryFindWall(orientation.forward, out hit)) {
            foundWall = pressingTowardWall = true;
        }
        else if (y < -0.1f && TryFindWall(-orientation.forward, out hit)) {
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

    private void ResetJump() => readyToJump = true;

    private void ResetSameWallCooldown() {
        sameWallOnCooldown = false;
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

            rb.AddForce(Vector3.up * jumpForce * 1.5f);
            
            rb.velocity = new Vector3(velocity.x, rb.velocity.y < 0.5f ? 0f : velocity.y / 2f, velocity.z);
        }
    }

    private void CounterMovement(float x, float y, Vector2 mag) {
        if (!grounded || jumping || wallRunning) return;

        //Slow down sliding
        float currentCounterMultiplier = crouching ? slideCounterMovement : counterMovement;

        //Counter movement
        if (Math.Abs(mag.x) > threshold && Math.Abs(x) < 0.05f || (mag.x < -threshold && x > 0) ||
            (mag.x > threshold && x < 0)) {
            rb.AddForce(moveSpeed * orientation.transform.right * NetworkSettings.tickTime * -mag.x * currentCounterMultiplier);
        }

        if (Math.Abs(mag.y) > threshold && Math.Abs(y) < 0.05f || (mag.y < -threshold && y > 0) ||
            (mag.y > threshold && y < 0)) {
            rb.AddForce(moveSpeed * orientation.transform.forward * NetworkSettings.tickTime * -mag.y *
                        currentCounterMultiplier);
        }

        if (crouching) return;

        //Limit diagonal running. This will also cause a full stop if sliding fast and un-crouching, so not optimal.
        Vector3 flatVel = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        if (flatVel.magnitude > runSpeed) {
            Vector3 limitedVel = flatVel.normalized * runSpeed;
            rb.velocity = new Vector3(limitedVel.x, rb.velocity.y, limitedVel.z);
        }
    }

    public Vector2 FindVelRelativeToLook() {
        float lookAngle = orientation.transform.eulerAngles.y;
        float moveAngle = Mathf.Atan2(rb.velocity.x, rb.velocity.z) * Mathf.Rad2Deg;

        float u = Mathf.DeltaAngle(lookAngle, moveAngle);
        float v = 90 - u;

        float magnitude = new Vector2(rb.velocity.x, rb.velocity.z).magnitude;
        return new Vector2(magnitude * Mathf.Cos(v * Mathf.Deg2Rad), magnitude * Mathf.Cos(u * Mathf.Deg2Rad));
    }

    private void CheckWallrunCancellation() {
        if (!wallRunning) {
            return;
        }

        float wallAngle =
            Vector3.SignedAngle(
                Vector3.forward,
                wallNormalVector,
                Vector3.up
            );

        float cameraAngle = orientation.eulerAngles.y;

        float wallAngleDiff =
            Mathf.DeltaAngle(cameraAngle, wallAngle);

        float wallRunRotation =
            (0f - wallAngleDiff / 90f) * 7.5f;

        if (!readyToWallrun)
            return;
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

    private bool IsSurf(Vector3 v) {
        float angle = Vector3.Angle(Vector3.up, v);

        if (angle < 89f) {
            return angle > maxSlopeAngle;
        }

        return false;
    }

    private bool IsWall(Vector3 v) {
        return Math.Abs(90f - Vector3.Angle(Vector3.up, v)) < 0.1f;
    }
}