using System;
using EZCameraShake;
using UnityEngine;
using Quaternion = UnityEngine.Quaternion;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

// ReSharper disable All

public class PlayerMovement : MonoBehaviour {
    //Assingables
    public Transform playerCam;
    public Transform orientation;

    //Other
    public Rigidbody rb;

    private readonly TickInvoker tickInvoker = new();

    //Rotation and look
    private float xRotation;
    private float sensitivity = 50f;
    private float sensMultiplier = 1f;

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
    private int jumpCooldown = 15;
    public float jumpForce = 550f;

    //Wallrunning & Surfing
    private bool wallRunning, surfing;
    private float wallRunRotation;
    private bool cancelling, cancellingWall, cancellingSurf;
    private bool readyToWallrun = true;
    private float wallRunGravity;
    private float actualWallRotation;
    private float wallRotationVel;
    private int cancelWallrunTimer = -1;
    private int wallrunCooldown = 10;

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

    //Effects
    public ParticleSystem ps;
    private ParticleSystem.EmissionModule psEmission;

    //Other
    private float fallSpeed;
    private Vector3 lastMoveSpeed;
    private CapsuleCollider playerCollider;

    public static PlayerMovement Instance { get; private set; }

    private void Awake() {
        Instance = this;
        rb = GetComponent<Rigidbody>();
        playerHeight = GetComponent<CapsuleCollider>().bounds.size.y;
    }

    private void Start() {
        psEmission = ps.emission;
        playerCollider = GetComponent<CapsuleCollider>();

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
        FindWallRunRotation();
        WallRunning();
        Movement();
    }

    private void MyInput() {
        float x = Input.GetAxisRaw("Horizontal");
        float y = Input.GetAxisRaw("Vertical");

        bool jumping = Input.GetButton("Jump");
        bool crouching = Input.GetButton("Crouch");

        PlayerMovement.Instance.SetInputs(x, y, jumping, crouching);

        if (Input.GetButtonDown("Crouch")) {
            PlayerMovement.Instance.StartCrouch();
        }

        if (Input.GetButtonUp("Crouch")) {
            PlayerMovement.Instance.StopCrouch();
        }
    }

    public void SetInputs(float x, float y, bool jumping, bool crouching) {
        if (crouching && !this.crouching) {
            // PlayerMovement.Instance.StartCrouch();
        }
        else if (!crouching && this.crouching) {
            // PlayerMovement.Instance.StopCrouch();
        }

        this.x = x;
        this.y = y;
        this.jumping = jumping;
        this.crouching = crouching;
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

        //If speed is larger than maxspeed, cancel out the input so you don't go over max speed
        if (x > 0 && xMag > maxSpeed) x = 0;
        if (x < 0 && xMag < -maxSpeed) x = 0;
        if (y > 0 && yMag > maxSpeed) y = 0;
        if (y < 0 && yMag < -maxSpeed) y = 0;

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
                    (y * moveSpeed * NetworkSettings.tickTime * multiplier * multiplierV));
        rb.AddForce(orientation.transform.right * (x * moveSpeed * NetworkSettings.tickTime * multiplier));

        SpeedLines();
    }

    public void StartCrouch() {
        transform.localScale = crouchScale;
        // transform.localPosition = new Vector3(transform.position.x, transform.position.y - 0.5f, transform.position.z);

        if (rb.velocity.magnitude > 0.1f && grounded)
            rb.AddForce(orientation.forward * 400f);
    }

    public void StopCrouch() {
        transform.localScale = playerScale;
        // transform.localPosition = new Vector3(transform.position.x, transform.position.y - 0.5f, transform.position.z);
    }

    private void SpeedLines() {
        float viewAngleFactor = Vector3.Angle(rb.velocity, playerCam.transform.forward) * 0.5f;
        if (viewAngleFactor < 1f) {
            viewAngleFactor = 1f;
        }

        float rateOverTimeMultiplier = rb.velocity.magnitude / viewAngleFactor;

        if (grounded && !wallRunning) {
            rateOverTimeMultiplier = 0f;
        }

        // psEmission.rateOverTimeMultiplier = rateOverTimeMultiplier;
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

        surfing = grounded && IsSurf(hit.normal);
        normalVector = hit.normal;

        if (!wasGrounded && grounded && MoveCamera.Instance != null) {
            MoveCamera.Instance.BobOnce(new Vector3(0f, fallSpeed, 0f));
        }
    }

    public void CheckWalls() {
        RaycastHit hit;

        Vector3 origin = transform.position;

        bool foundWall =
            Physics.Raycast(origin, orientation.right, out hit, 1f, whatIsGround) ||
            Physics.Raycast(origin, -orientation.right, out hit, 1f, whatIsGround) ||
            Physics.Raycast(origin, orientation.forward, out hit, 1f, whatIsGround) ||
            Physics.Raycast(origin, -orientation.forward, out hit, 1f, whatIsGround);

        if (!foundWall) {
            wallRunning = false;
            return;
        }

        if (!IsWall(hit.normal)) {
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

    private void CameraShake() {
        float shakeStrength = rb.velocity.magnitude / 12f;

        CameraShaker.Instance.ShakeOnce(shakeStrength, 0.025f * shakeStrength, 0.25f, 0.2f);
        Invoke("CameraShake", 0.2f);
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
            }

            tickInvoker.Invoke(ResetJump, jumpCooldown);
            if (wallRunning) {
                wallRunning = false;
            }
        }
    }

    public void Look() {
        float x = Input.GetAxis("Mouse X") * sensitivity * Time.fixedDeltaTime * sensMultiplier;
        float y = Input.GetAxis("Mouse Y") * sensitivity * Time.fixedDeltaTime * sensMultiplier;

        desiredX = playerCam.transform.localRotation.eulerAngles.y + x;
        xRotation -= y;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        actualWallRotation = Mathf.SmoothDamp(actualWallRotation, wallRunRotation, ref wallRotationVel, 0.2f);

        cameraRot = new Vector3(xRotation, desiredX, actualWallRotation);
        playerCam.transform.localRotation = Quaternion.Euler(xRotation, desiredX, actualWallRotation);
        orientation.transform.localRotation = Quaternion.Euler(0f, desiredX, 0f);
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

    private void FindWallRunRotation() {
        if (!wallRunning) {
            wallRunRotation = 0f;
            return;
        }

        _ = new Vector3(0f, playerCam.transform.rotation.y, 0f).normalized;
        new Vector3(0f, 0f, 1f);
        float wallAngle = 0f;
        float cameraAngle = playerCam.transform.rotation.eulerAngles.y;

        if (Math.Abs(wallNormalVector.x - 1f) < 0.1f) {
            wallAngle = 90f;
        }
        else if (Math.Abs(wallNormalVector.x - -1f) < 0.1f) {
            wallAngle = 270f;
        }
        else if (Math.Abs(wallNormalVector.z - 1f) < 0.1f) {
            wallAngle = 0f;
        }
        else if (Math.Abs(wallNormalVector.z - -1f) < 0.1f) {
            wallAngle = 180f;
        }

        wallAngle = Vector3.SignedAngle(new Vector3(0f, 0f, 1f), wallNormalVector, Vector3.up);
        float wallAngleDiff = Mathf.DeltaAngle(cameraAngle, wallAngle);
        wallRunRotation = (0f - wallAngleDiff / 90f) * 7.5f;
        if (!readyToWallrun) {
            return;
        }

        if ((Mathf.Abs(wallRunRotation) < 4f && y > 0f && Math.Abs(x) < 0.1f) ||
            (Mathf.Abs(wallRunRotation) > 22f && y < 0f && Math.Abs(x) < 0.1f)) {
            if (!cancelling) {
                cancelling = true;
                tickInvoker.Cancel(cancelWallrunTimer);
                cancelWallrunTimer =
                    tickInvoker.Invoke(CancelWallrun, wallrunCooldown);
            }
        }
        else {
            cancelling = false;
            tickInvoker.Cancel(CancelWallrun);
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