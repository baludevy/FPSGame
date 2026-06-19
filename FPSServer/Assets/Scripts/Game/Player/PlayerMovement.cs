using System;
using UnityEngine;

// ReSharper disable All

public class PlayerMovement : MonoBehaviour {
    //Assignables
    public Transform orientation;
    public LayerMask whatIsGround;
    public Player player;
    private Rigidbody rb;
    private CapsuleCollider playerCollider;

    //Ground check
    private bool grounded;
    private bool wasGrounded;

    [Header("Movement")] public float acceleration = 4000f;
    public float maxRunSpeed = 12f;

    public float counterMovement = 0.3f;
    public float threshold = 0.01f;
    public float maxSlopeAngle = 35f;

    public float jumpForce = 10;
    private bool jumpedThisFrame;

    [Header("Air movement")] public float airAcceleration = 20f;
    public float maxAirSpeed = 16f;

    [Header("Sliding")] public float slideBoost = 6f;
    public float maxSlideSpeed = 22f;
    public float slideFriction = 1.2f;
    public float slideStopSpeed = 2f;
    public float slideCooldown = 1.5f;
    public float slideMinSpeed = 4f;
    public float slideEndSpeed = 3f;
    private bool slideCooldownActive;
    private int cancelSlideCooldownAction;

    [Header("Wallrunning")] public float wallRunSpeed = 35f;
    public float wallRunAcceleration = 3000f;
    public float wallRunMaxFallSpeed = -1f;
    public float wallRunIdleMaxFallSpeed = -0.6f;
    public float wallRunDistance = 1.5f;
    public float initialWallBoost = 12f;
    public float wallKickImpulse = 10f;
    public float wallKickParallelImpulse = 10f;
    public float wallKickInwardImpulse = 3f;
    public float sameWallCooldown = 0.3f;
    public float wallRunMaxTime = 3f;
    private int cancelWallRunAction;
    private bool wallRunning, startingWallRun;

    private bool sliding;
    private bool airSlide;
    private Vector3 normalVector = Vector3.up;
    private Vector3 wallNormalVector;

    private Vector3 crouchVel;
    private Vector3 targetScale;

    [Header("Crouching")] public Vector3 crouchScale = new Vector3(1.25f, 1f, 1.25f);
    public float maxCrouchSpeed = 5f;

    //Input state
    private float x, y;
    private bool wasJumping, jumping, wasCrouching, crouching;

    private float maxSpeed;
    private float lastFallSpeed;
    private Vector3 baseScale;

    private Vector3 cameraRot;
    private float desiredX;

    private void Awake() {
        rb = GetComponent<Rigidbody>();
        playerCollider = GetComponent<CapsuleCollider>();

        baseScale = transform.localScale;
        targetScale = baseScale;
    }

    private void Start() {
    }

    public void AdvanceLogic() {
        CheckGrounded();
        CheckWalls();
        Movement();

        wasGrounded = grounded;
        lastFallSpeed = rb.velocity.y;
        wasJumping = jumping;
        wasCrouching = crouching;
    }

    public void SetInput(InputData inp) {
        if (inp.crouching && !crouching) {
            StartCrouch();
        }
        else if (!inp.crouching && crouching) {
            StopCrouch();
        }

        x = inp.x;
        y = inp.y;
        orientation.localRotation = Quaternion.Euler(0f, inp.yaw, 0f);
        jumping = inp.jumping;
        crouching = inp.crouching;
    }

    private void Movement() {
        jumpedThisFrame = false;

        //stop sliding
        float groundVel = new Vector3(rb.velocity.x, 0f, rb.velocity.z).magnitude;
        if (sliding && (!crouching || groundVel < slideEndSpeed)) {
            sliding = false;
        }

        ExitWallRunning();

        if (jumping) {
            Jump();
        }

        //landed this frame
        if (!wasGrounded && grounded) {
            OnLanding();
        }

        if (startingWallRun) {
            rb.AddForce(-wallNormalVector, ForceMode.Acceleration);
        }

        if (wallRunning) {
            WallRunning();
            return;
        }

        if (!grounded) {
            if (!wallRunning && IsNearWall() && IsPressingTowardWall(wallNormalVector))
                StartWallRun();

            AirMovement();
            return;
        }

        if (sliding) {
            SlideFriction();

            wasJumping = jumping;
            wasCrouching = crouching;
            return;
        }

        float mult = 1f;
        maxSpeed = maxRunSpeed;

        if (crouching) {
            mult = 0.3f;
            maxSpeed = maxCrouchSpeed;
        }

        // ground movement
        // prevent ground friction from acting to preserve momentum on landing frame
        if (wasGrounded) {
            //get velocity relative to where the player is looking
            Vector2 mag = FindVelRelativeToLook();

            Vector3 moveRight = Vector3.ProjectOnPlane(orientation.right, normalVector);
            Vector3 moveForward = Vector3.ProjectOnPlane(orientation.forward, normalVector);

            if (moveRight.sqrMagnitude > 0.0001f) moveRight = moveRight.normalized;
            if (moveForward.sqrMagnitude > 0.0001f) moveForward = moveForward.normalized;

            CounterMovement(mag, moveRight, moveForward);

            rb.AddForce(moveRight * x * acceleration * NetworkSettings.tickTime * mult);
            rb.AddForce(moveForward * y * acceleration * NetworkSettings.tickTime * mult);

            rb.velocity = Vector3.ProjectOnPlane(rb.velocity, normalVector);
            rb.AddForce(-normalVector, ForceMode.Acceleration);

            if (Mathf.Abs(x) < threshold && Mathf.Abs(y) < threshold) {
                Vector3 gravityAlongSlope = Vector3.ProjectOnPlane(Physics.gravity, normalVector);
                rb.AddForce(-gravityAlongSlope - normalVector, ForceMode.Acceleration);
            }
        }
    }

    private void AirMovement() {
        Vector3 vel = rb.velocity;

        Vector3 wishDir = orientation.right * x + orientation.forward * y;
        wishDir.y = 0f;
        if (wishDir.sqrMagnitude > 0.001f)
            wishDir.Normalize();
        else
            return;

        //how fast were already going in the wish direction
        float currentSpeed = Vector3.Dot(vel, wishDir);

        //how much headroom we have before we hit the cap IN THAT DIRECTION
        float addSpeed = maxAirSpeed - currentSpeed;
        if (addSpeed <= 0f) return;

        float accelSpeed = airAcceleration * NetworkSettings.tickTime;
        accelSpeed = Mathf.Min(accelSpeed, addSpeed);

        rb.AddForce(wishDir * accelSpeed, ForceMode.VelocityChange);
    }

    private void OnLanding() {
        ResetWallRun();

        float speed = new Vector3(rb.velocity.x, 0f, rb.velocity.z).magnitude;

        if (airSlide) {
            airSlide = false;
            sliding = true;

            if (speed <= slideBoost)
                Slide();
        }
    }

    private void Jump() {
        if (wallRunning || startingWallRun) {
            WallKick();
            return;
        }

        if (!grounded) return;

        jumpedThisFrame = true;
        Vector3 vel = rb.velocity;
        Vector3 flatVel = Vector3.ProjectOnPlane(vel, normalVector);
        rb.velocity = flatVel;

        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    }

    private void StartCrouch() {
        transform.localScale = crouchScale;
        if (grounded) {
            float heightDiff = baseScale.y - crouchScale.y;
            transform.localPosition = new Vector3(transform.position.x, transform.position.y - heightDiff,
                transform.position.z);
        }

        float groundVel = new Vector3(rb.velocity.x, 0, rb.velocity.z).magnitude;

        if (groundVel > slideMinSpeed && grounded)
            Slide();
        else if (!grounded)
            airSlide = true;
    }

    private void StopCrouch() {
        transform.localScale = baseScale;
        if (grounded) {
            float heightDiff = baseScale.y - crouchScale.y;
            transform.localPosition = new Vector3(transform.position.x, transform.position.y + heightDiff,
                transform.position.z);
        }

        airSlide = false;
    }

    private void Slide() {
        Vector3 slopeVel = Vector3.ProjectOnPlane(rb.velocity, normalVector);
        float speed = slopeVel.magnitude;

        if (speed < 0.01f || slideCooldownActive) return;

        sliding = true;
        slideCooldownActive = true;
        player.invoker.Cancel(cancelSlideCooldownAction);
        cancelSlideCooldownAction = player.invoker.Invoke(EndSlideCooldown, TickUtil.SecondsToTick(slideCooldown));

        Vector3 dir = slopeVel / speed;

        if (y < 0f) {
            return;
        }

        float add = Mathf.Clamp(maxSlideSpeed - speed, 0f, slideBoost);

        rb.AddForce(dir * add, ForceMode.VelocityChange);
    }

    private void EndSlideCooldown() {
        slideCooldownActive = false;
    }

    private void SlideFriction() {
        Vector3 vel = rb.velocity;

        Vector3 slopeDir = Vector3.ProjectOnPlane(vel, normalVector);

        float speed = slopeDir.magnitude;
        if (speed < 0.01f) return;

        float control = Mathf.Max(speed, slideStopSpeed);
        float drop = control * slideFriction * NetworkSettings.tickTime;
        float newSpeed = Mathf.Max(speed - drop, 0f);

        Vector3 newVel = slopeDir.normalized * newSpeed;

        newVel.y = vel.y;

        rb.velocity = newVel;
    }

    // called when wall is found
    private void PreWallRun() {
        startingWallRun = true;
    }

    // called on wall contact
    private void StartWallRun() {
        startingWallRun = false;
        wallRunning = true;

        cancelWallRunAction = player.invoker.Invoke(ForceExitWallRun, TickUtil.SecondsToTick(wallRunMaxTime));

        Vector3 vel = rb.velocity;
        Vector3 flatVel = new Vector3(vel.x, 0, vel.z);

        float wallDot = Vector3.Dot(flatVel, wallNormalVector);
        flatVel -= wallDot * wallNormalVector;

        Vector3 camFlatDir = Vector3.ProjectOnPlane(orientation.forward, wallNormalVector);
        if (camFlatDir.sqrMagnitude > 0.001f) {
            camFlatDir.Normalize();
            float camDot = Vector3.Dot(flatVel, camFlatDir);
            if (camDot < 0f) {
                flatVel -= camDot * 0.8f * camFlatDir;
            }
        }

        rb.velocity = flatVel;

        if (!jumping)
            rb.AddForce(Vector3.up * initialWallBoost, ForceMode.Impulse);
    }

    // called while in contact with wall
    public void WallRunning() {
        if (!wallRunning) return;

        rb.AddForce(-wallNormalVector * NetworkSettings.tickTime * acceleration);

        Vector3 flatVel = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        float currentSpeed = flatVel.magnitude;

        Vector3 camFlatDir = Vector3.ProjectOnPlane(orientation.forward, wallNormalVector);
        Vector3 flatDir = camFlatDir.sqrMagnitude > 0.001f
            ? camFlatDir.normalized
            : (flatVel.sqrMagnitude > 0.001f ? flatVel.normalized : orientation.forward);

        if (currentSpeed < wallRunSpeed && y > threshold) {
            float speedDelta = wallRunSpeed - currentSpeed;
            float accel = Mathf.Min(speedDelta, wallRunAcceleration * NetworkSettings.tickTime);

            rb.AddForce(flatDir * accel);
        }
        else if (currentSpeed > wallRunSpeed) {
            rb.velocity -= flatVel * 2f * NetworkSettings.tickTime;
        }

        if (!jumping && IsPressingTowardWall(wallNormalVector) && rb.velocity.y < wallRunMaxFallSpeed)
            rb.velocity = new Vector3(rb.velocity.x, wallRunMaxFallSpeed, rb.velocity.z);
        else if (!jumping && rb.velocity.y < wallRunIdleMaxFallSpeed)
            rb.velocity = new Vector3(rb.velocity.x, wallRunIdleMaxFallSpeed, rb.velocity.z);
    }

    private void WallKick() {
        Vector3 wishDir = (orientation.forward * y + orientation.right * x).normalized;

        Vector3 impulse = wallNormalVector * wallKickImpulse;

        Vector3 wallParallel = Vector3.Cross(wallNormalVector, Vector3.up).normalized;
        float parallelDot = Vector3.Dot(wishDir, wallParallel);
        impulse += wallParallel * parallelDot * wallKickParallelImpulse;

        float inwardDot = Vector3.Dot(wishDir, -wallNormalVector);
        if (inwardDot > 0f) {
            impulse += wallNormalVector * (inwardDot * inwardDot) * wallKickInwardImpulse;
        }

        impulse += Vector3.up * jumpForce;

        rb.velocity = new Vector3(rb.velocity.x, Mathf.Min(rb.velocity.y, 0f), rb.velocity.z);

        rb.AddForce(impulse, ForceMode.Impulse);
        ResetWallRun();
    }

    private void ForceExitWallRun() {
        Vector3 wishDir = (orientation.forward * y + orientation.right * x).normalized;

        Vector3 impulse = wallNormalVector * wallKickImpulse * 2f;

        Vector3 wallParallel = Vector3.Cross(wallNormalVector, Vector3.up).normalized;
        float parallelDot = Vector3.Dot(wishDir, wallParallel);
        impulse += wallParallel * parallelDot * wallKickParallelImpulse;

        rb.velocity = new Vector3(rb.velocity.x, Mathf.Min(rb.velocity.y, 0f), rb.velocity.z);

        rb.AddForce(impulse, ForceMode.Impulse);
        ResetWallRun();
    }

    private void ExitWallRunning() {
        if (grounded || IsNearWall())
            return;

        ResetWallRun();
    }

    private void ResetWallRun() {
        wallRunning = false;
        startingWallRun = false;
        rb.useGravity = true;
        player.invoker.Cancel(cancelWallRunAction);
    }

    private bool IsNearWall() {
        Vector3[] directions = { orientation.right, -orientation.right, orientation.forward, -orientation.forward };
        return TryFindWall(transform.position, directions, 1.05f, out _);
    }

    private bool IsPressingTowardWall(Vector3 wallNormal) {
        Vector3 inputDir = orientation.right * x + orientation.forward * y;
        return Vector3.Dot(inputDir, -wallNormal) > 0f;
    }

    private bool IsPressingAwayFromWall(Vector3 wallNormal) {
        Vector3 inputDir = orientation.right * x + orientation.forward * y;
        return Vector3.Dot(inputDir, wallNormal) > 0f;
    }

    private void CounterMovement(Vector2 mag, Vector3 slopeRight, Vector3 slopeForward) {
        if (jumpedThisFrame) return;

        Vector3 counterForce = Vector3.zero;

        if (ShouldCounter(mag.x, x))
            counterForce += -slopeRight * mag.x;

        if (ShouldCounter(mag.y, y))
            counterForce += -slopeForward * mag.y;

        rb.AddForce(counterForce * acceleration * counterMovement * NetworkSettings.tickTime);

        if (!sliding) {
            Vector3 vel = rb.velocity;
            Vector3 flatVel = new Vector3(vel.x, 0, vel.z);

            if (flatVel.magnitude < 0.05f) {
                rb.velocity = new Vector3(0f, vel.y, 0f);
            }
        }

        LimitSpeed();
    }

    private bool ShouldCounter(float input, float velocityAxis) {
        return (Mathf.Abs(input) > threshold && Mathf.Abs(velocityAxis) < 0.05f)
               || (input < -threshold && velocityAxis > 0)
               || (input > threshold && velocityAxis < 0);
    }

    private void LimitSpeed() {
        if (sliding) return;

        Vector3 vel = rb.velocity;

        Vector3 flatVel = new Vector3(vel.x, 0, vel.z);

        if (flatVel.sqrMagnitude > maxSpeed * maxSpeed) {
            Vector3 limited = flatVel.normalized * maxSpeed;
            Vector3 normalComponent = Vector3.Project(vel, normalVector);
            rb.velocity = limited + normalComponent;
        }
    }

    private void CheckGrounded() {
        Vector3 scale = transform.lossyScale;
        float worldRadius = playerCollider.radius * Mathf.Max(scale.x, scale.z) * 0.9f;
        float worldHeight = playerCollider.height * scale.y;
        Vector3 center = transform.position + Vector3.Scale(playerCollider.center, scale);

        float tolerance = 0.2f;
        float maxDistance = worldHeight * 0.5f - worldRadius + tolerance;

        RaycastHit hit;
        bool didHit = Physics.SphereCast(
            center, worldRadius, Vector3.down,
            out hit, maxDistance, whatIsGround,
            QueryTriggerInteraction.Ignore
        );

        grounded = didHit && Vector3.Angle(hit.normal, Vector3.up) < maxSlopeAngle;
        normalVector = grounded ? hit.normal : Vector3.up;
    }

    public void CheckWalls() {
        if (grounded || wallRunning) return;

        Vector3 origin = transform.position;
        Vector3[] directions = { orientation.right, -orientation.right, orientation.forward };

        RaycastHit hit;
        if (TryFindWall(origin, directions, wallRunDistance, out hit)) {
            wallNormalVector = hit.normal;
            Vector3 flatVel = Vector3.ProjectOnPlane(rb.velocity, normalVector);

            Vector3 toWall = -hit.normal;
            float approachSpeed = Vector3.Dot(rb.velocity, toWall);

            if (flatVel.magnitude > 1f && approachSpeed > 0.5f) {
                if (hit.distance > 1f && !startingWallRun) {
                    PreWallRun();
                }
                else if (hit.distance <= 1f) {
                    StartWallRun();
                }
            }
        }
    }

    private bool TryFindWall(Vector3 origin, Vector3[] directions, float distance, out RaycastHit hit) {
        foreach (var dir in directions) {
            if (Physics.Raycast(origin, dir, out hit, distance, whatIsGround) && IsWall(hit.normal))
                return true;
        }

        hit = default;
        return false;
    }

    private bool ShouldStartWallRun(Vector3 flatVel, bool foundWall, Vector3 normal, out bool valid) {
        valid = false;

        if (!foundWall || wallRunning || flatVel.magnitude <= 1f || grounded)
            return false;

        Vector3 toWall = -normal;
        float approachSpeed = Vector3.Dot(rb.velocity, toWall);

        valid = approachSpeed > 0.5f;
        return true;
    }

    public Vector2 FindVelRelativeToLook() {
        float lookAngle = orientation.eulerAngles.y;
        float moveAngle = Mathf.Atan2(rb.velocity.x, rb.velocity.z) * Mathf.Rad2Deg;
        float u = Mathf.DeltaAngle(lookAngle, moveAngle);
        float v = 90 - u;
        float magnitude = new Vector2(rb.velocity.x, rb.velocity.z).magnitude;

        return new Vector2(magnitude * Mathf.Cos(v * Mathf.Deg2Rad), magnitude * Mathf.Cos(u * Mathf.Deg2Rad));
    }

    private bool IsFloor(Vector3 v) {
        float angle = Vector3.Angle(Vector3.up, v);
        return angle < maxSlopeAngle;
    }

    private bool IsWall(Vector3 v) {
        float angle = Vector3.Angle(Vector3.up, v);
        return angle >= 80f && angle <= 100f;
    }

    public Rigidbody GetRb() {
        return rb;
    }

    public Transform GetOrientation() {
        return orientation;
    }

    public float GetFallSpeed() {
        return rb.velocity.y;
    }

    public bool IsGrounded() {
        return grounded;
    }

    public bool IsWallRunning() {
        return wallRunning || startingWallRun;
    }

    public bool IsCrouching() {
        return crouching;
    }

    public bool IsSliding() {
        return sliding;
    }

    public Vector2 GetCameraRot() {
        return new Vector2(cameraRot.x, desiredX);
    }
}