using System;
using UnityEngine;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

// ReSharper disable All

public class PlayerMovement : MonoBehaviour {
    //Assingables
    public Transform orientation;

    //Other
    public Rigidbody rb;

    //Movement
    public float moveSpeed = 4500;
    public float runSpeed = 20;
    public bool grounded, wasGrounded, cancellingGrounded;
    public LayerMask whatIsGround;

    public float counterMovement = 0.175f;
    private float threshold = 0.01f;
    public float maxSlopeAngle = 35f;

    //Crouch & Slide
    private Vector3 crouchScale = new Vector3(1, 1f, 1);
    private Vector3 playerScale = new Vector3(1f, 1.5f, 1);
    public float slideForce = 400;
    public float slideCounterMovement = 0.2f;

    //Jumping
    private bool readyToJump = true;
    private int jumpCooldown = 5;
    public float jumpForce = 550f;

    //Wallrunning & Surfing
    private bool wallRunning, surfing;
    private float wallRunRotation;
    private bool cancelling, cancellingWall, cancellingSurf;
    private bool readyToWallrun = true;
    private float wallRunGravity = 1f;
    private float actualWallRotation;
    private float wallRotationVel;
    private int wallrunCooldown = 5;
    private int cancelWallrunTimer = -1;

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
    private float fallSpeed;
    private Vector3 lastMoveSpeed;
    private CapsuleCollider playerCollider;

    private readonly TickInvoker tickInvoker = new();

    private void Awake() {
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

        float xMag = mag.x;
        float yMag = mag.y;

        CounterMovement(x, y, mag);

        if (readyToJump && jumping) {
            Jump();
        }

        float maxSpeed = runSpeed;

        if (crouching && grounded && readyToJump) {
            rb.AddForce(Vector3.down * NetworkSettings.tickTime * 3000f);
            return;
        }

        float inputX = x;
        float inputY = y;

        //If speed is larger than maxspeed, cancel out the input so you don't go over max speed
        if (inputX > 0 && xMag > maxSpeed) inputX = 0;
        if (inputX < 0 && xMag < -maxSpeed) inputX = 0;
        if (inputY > 0 && yMag > maxSpeed) inputY = 0;
        if (inputY < 0 && yMag < -maxSpeed) inputY = 0;

        float multiplier = 1f;
        float multiplierV = 1f;

        // air movement
        if (!grounded) {
            multiplier = 0.55f;
            multiplierV = 0.55f;
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

        rb.AddForce(orientation.transform.forward *
                    (inputY * moveSpeed * NetworkSettings.tickTime * multiplier * multiplierV));
        rb.AddForce(orientation.transform.right * (inputX * moveSpeed * NetworkSettings.tickTime * multiplier));
    }
    //Scale player down
    private void StartCrouch() {
        transform.localScale = new Vector3(1.5f, 1f, 1.5f);
        transform.localPosition = new Vector3(transform.position.x, transform.position.y - 1f,
            transform.position.z);
        
        playerHeight = 2f;

        /* if (rb.velocity.magnitude > 0.1f && grounded) {
            rb.AddForce(orientation.transform.forward * 400f);
        } */
    }

    //Scale player to original size
    private void StopCrouch() {
        transform.localScale = new Vector3(1.5f, 2f, 1.5f);
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
        }
        else {
            normalVector = Vector3.up;
            surfing = false;
        }
    }

    public void CheckWalls() {
        RaycastHit hit = default;
        bool foundWall = false;

        Vector3 origin = transform.position;

        RaycastHit tmp;
        if (Physics.Raycast(origin, orientation.right, out tmp, 1f, whatIsGround) && IsWall(tmp.normal)) {
            hit = tmp;
            foundWall = true;
        }
        else if (Physics.Raycast(origin, -orientation.right, out tmp, 1f, whatIsGround) && IsWall(tmp.normal)) {
            hit = tmp;
            foundWall = true;
        }
        else if (Physics.Raycast(origin, orientation.forward, out tmp, 1f, whatIsGround) && IsWall(tmp.normal)) {
            hit = tmp;
            foundWall = true;
        }
        else if (Physics.Raycast(origin, -orientation.forward, out tmp, 1f, whatIsGround) && IsWall(tmp.normal)) {
            hit = tmp;
            foundWall = true;
        }

        if (!foundWall) {
            wallRunning = false;
            return;
        }

        wallNormalVector = hit.normal;

        if (!grounded && readyToWallrun) {
            if (!wallRunning) {
                rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
                rb.AddForce(Vector3.up * 15, ForceMode.Impulse);
            }

            wallRunning = true;
        }
        else {
            wallRunning = false;
        }
    }

    private void ResetJump() => readyToJump = true;

    private void Jump() {
        if ((grounded || wallRunning || surfing) && readyToJump) {
            Vector3 velocity = rb.velocity;
            readyToJump = false;
            rb.AddForce(Vector2.up * jumpForce * 1.5f);
            rb.AddForce(normalVector * jumpForce * 0.5f);
            if (rb.velocity.y < 0.5f) {
                rb.velocity = new Vector3(velocity.x, 0f, velocity.z);
            }
            else if (rb.velocity.y > 0f) {
                rb.velocity = new Vector3(velocity.x, velocity.y / 2f, velocity.z);
            }

            if (wallRunning) {
                rb.AddForce(wallNormalVector * jumpForce * 3f);

                wallRunning = false;
            }

            tickInvoker.Invoke(ResetJump, jumpCooldown);
            if (wallRunning) {
                wallRunning = false;
            }
        }
    }

    private void CounterMovement(float x, float y, Vector2 mag) {
        if (!grounded || jumping) return;

        //Slow down sliding
        if (crouching) {
            rb.AddForce(moveSpeed * NetworkSettings.tickTime * -rb.velocity.normalized * slideCounterMovement);
            return;
        }

        //Counter movement
        if (Math.Abs(mag.x) > threshold && Math.Abs(x) < 0.05f || (mag.x < -threshold && x > 0) ||
            (mag.x > threshold && x < 0)) {
            rb.AddForce(moveSpeed * orientation.transform.right * NetworkSettings.tickTime * -mag.x * counterMovement);
        }

        if (Math.Abs(mag.y) > threshold && Math.Abs(y) < 0.05f || (mag.y < -threshold && y > 0) ||
            (mag.y > threshold && y < 0)) {
            rb.AddForce(moveSpeed * orientation.transform.forward * NetworkSettings.tickTime * -mag.y *
                        counterMovement);
        }

        //Limit diagonal running. This will also cause a full stop if sliding fast and un-crouching, so not optimal.
        if (Mathf.Sqrt((Mathf.Pow(rb.velocity.x, 2) + Mathf.Pow(rb.velocity.z, 2))) > runSpeed) {
            float fallspeed = rb.velocity.y;
            Vector3 n = rb.velocity.normalized * runSpeed;
            rb.velocity = new Vector3(n.x, fallspeed, n.z);
        }
    }

    public Vector2 FindVelRelativeToLook() {
        float lookAngle = orientation.transform.eulerAngles.y;
        float moveAngle = Mathf.Atan2(rb.velocity.x, rb.velocity.z) * Mathf.Rad2Deg;

        float u = Mathf.DeltaAngle(lookAngle, moveAngle);
        float v = 90 - u;

        float magnitue = rb.velocity.magnitude;
        float yMag = magnitue * Mathf.Cos(u * Mathf.Deg2Rad);
        float xMag = magnitue * Mathf.Cos(v * Mathf.Deg2Rad);

        return new Vector2(xMag, yMag);
    }

    private void CheckWallrunCancellation() {
        if (!wallRunning) {
            cancelling = false;
            tickInvoker.Cancel(cancelWallrunTimer);
            cancelWallrunTimer = -1;
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

        bool shouldCancel =
            (Mathf.Abs(wallRunRotation) < 4f &&
             y > 0f &&
             Mathf.Abs(x) < 0.1f)
            ||
            (Mathf.Abs(wallRunRotation) > 22f &&
             y < 0f &&
             Mathf.Abs(x) < 0.1f);

        if (shouldCancel) {
            if (!cancelling) {
                cancelling = true;

                tickInvoker.Cancel(cancelWallrunTimer);

                cancelWallrunTimer =
                    tickInvoker.Invoke(
                        CancelWallrun,
                        wallrunCooldown
                    );
            }
        }
        else {
            cancelling = false;
            tickInvoker.Cancel(cancelWallrunTimer);
            cancelWallrunTimer = -1;
        }
    }

    private void CancelWallrun() {
        tickInvoker.Invoke(GetReadyToWallrun, wallrunCooldown);
        rb.AddForce(wallNormalVector * 600f);
        readyToWallrun = false;
    }

    private void GetReadyToWallrun() {
        readyToWallrun = true;
    }

    public void WallRunning() {
        if (wallRunning) {
            rb.AddForce(-wallNormalVector * NetworkSettings.tickTime * moveSpeed);
            if (!isCrouching) {
                rb.AddForce(Vector3.up * NetworkSettings.tickTime * rb.mass * 100f * wallRunGravity);
            }
            else {
                rb.AddForce(Vector3.up * NetworkSettings.tickTime * rb.mass * 100f * (wallRunGravity * 0.5f));
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