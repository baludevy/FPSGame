using UnityEngine;

public class PlayerMovement : MonoBehaviour {
    public Transform orientation;
    public LayerMask whatIsGround;
    private Rigidbody rb;
    private CapsuleCollider playerCollider;

    private bool grounded;
    private bool wasGrounded;

    [Header("Movement")] public float acceleration = 4000f;
    public float maxRunSpeed = 12f;
    public float counterMovement = 0.3f;
    public float threshold = 0.01f;
    public float maxSlopeAngle = 35f;
    public float jumpForce = 10f;

    [Header("Air movement")] public float airAcceleration = 20f;
    public float maxAirSpeed = 16f;

    [Header("Sliding")] public float slideBoost = 6f;
    public float maxSlideSpeed = 22f;
    public float slideSlopeAccel = 2.8f;
    public float slideFriction = 1.2f;
    public float slideStopSpeed = 2f;
    public float slideCooldown = 1.5f;
    public float slideMinSpeed = 4f;
    public float slideEndSpeed = 3f;

    [Header("Wallrunning")] public float wallRunSpeed = 35f;
    public float wallRunAcceleration = 3000f;
    public float wallRunMaxFallSpeed = -1f;
    public float wallRunIdleMaxFallSpeed = -0.6f;
    public float wallRunDistance = 1.5f;
    public float wallRunCameraTilt = 9f;
    public float wallRunCameraTiltSmooth = 0.1f;
    public float initialWallBoost = 12f;
    public float wallKickImpulse = 10f;
    public float wallRunMaxTime = 3f;

    [Header("Crouching")] public Vector3 crouchScale = new Vector3(1.25f, 1f, 1.25f);
    public float maxCrouchSpeed = 5f;
    public float crouchTransitionSpeed = 10f;

    private bool sliding;
    private bool airSlide;
    private Vector3 normalVector = Vector3.up;
    private Vector3 wallNormalVector;

    private float x, y;
    private bool jumping, crouching;
    private bool jumpedThisFrame;

    private float maxSpeed;
    private float lastFallSpeed;
    private Vector3 baseScale;

    private bool slideCooldownActive;
    private int cancelSlideCooldownAction;

    private bool wallRunning, startingWallRun, preWallRunning;
    private float wallRunRotation;
    private float actualWallRotation;
    private float wallRotationVel;
    private int cancelWallRunAction;
    private float crouchAmount;

    private void Awake() {
        rb = GetComponent<Rigidbody>();
        playerCollider = GetComponent<CapsuleCollider>();
        baseScale = transform.localScale;
    }

    private void Start() {
        CursorManager.DisableCursor();
    }

    public void SetInput(InputData input) {
        bool inputCrouching = (input.buttons & Buttons.Crouch) != 0;
        bool inputJumping = (input.buttons & Buttons.Jump) != 0;

        if (inputCrouching && !crouching) {
            StartCrouch();
        }
        else if (!inputCrouching && crouching) {
            StopCrouch();
        }

        x = input.x;
        y = input.y;
        orientation.localRotation = Quaternion.Euler(0f, input.yaw, 0f);
        jumping = inputJumping;
        crouching = inputCrouching;
    }

    public void AdvanceLogic() {
        CheckGrounded();
        CheckWalls();
        FindWallRunRotation();
        UpdateCrouchScale();
        Movement();

        wasGrounded = grounded;
        lastFallSpeed = rb.velocity.y;
    }

    private void LateUpdate() {
        actualWallRotation = Mathf.SmoothDamp(actualWallRotation, wallRunRotation, ref wallRotationVel,
            wallRunCameraTiltSmooth);

        LocalPlayer.Instance.playerCamera.wallRotation = actualWallRotation;
    }

    private void Movement() {
        jumpedThisFrame = false;

        // stop sliding if no longer crouching or too slow
        float groundVel = new Vector3(rb.velocity.x, 0f, rb.velocity.z).magnitude;
        if (sliding && (!crouching || groundVel < slideEndSpeed)) {
            sliding = false;
        }

        ExitWallRunning();

        if (jumping) {
            Jump();
        }

        if (!wasGrounded && grounded) {
            OnLanding();
        }

        // only start wallrun on next tick
        if (startingWallRun && !wallRunning) {
            wallRunning = true;
            startingWallRun = false;
        }

        if (wallRunning) {
            WallRunning();
            return;
        }

        if (!grounded) {
            if (!preWallRunning && !startingWallRun && !wallRunning && IsTouchingWall(-wallNormalVector) &&
                IsPressingTowardWall(wallNormalVector)) {
                StartWallRun();
            }

            AirMovement();
            return;
        }

        if (sliding) {
            SlideFriction();
            return;
        }

        float mult = 1f;
        maxSpeed = maxRunSpeed;

        if (crouching) {
            mult = 0.3f;
            maxSpeed = maxCrouchSpeed;
        }

        if (wasGrounded) {
            GroundMovement(mult);
        }
    }

    private void GroundMovement(float mult) {
        Vector2 mag = FindVelRelativeToLook();

        Vector3 moveRight = Vector3.ProjectOnPlane(orientation.right, normalVector);
        Vector3 moveForward = Vector3.ProjectOnPlane(orientation.forward, normalVector);

        if (moveRight.sqrMagnitude > 0.0001f) moveRight = moveRight.normalized;
        if (moveForward.sqrMagnitude > 0.0001f) moveForward = moveForward.normalized;

        CounterMovement(mag, moveRight, moveForward);

        rb.AddForce(moveRight * x * acceleration * NetworkSettings.tickTime * mult);
        rb.AddForce(moveForward * y * acceleration * NetworkSettings.tickTime * mult);

        // cancel any velocity component along the normal to keep movement on the slope plane
        float normalVel = Vector3.Dot(rb.velocity, normalVector);
        if (normalVel > 0f) {
            rb.AddForce(-normalVector * normalVel, ForceMode.VelocityChange);
        }

        rb.AddForce(-normalVector, ForceMode.Acceleration);

        if (Mathf.Abs(x) < threshold && Mathf.Abs(y) < threshold) {
            Vector3 gravityAlongSlope = Vector3.ProjectOnPlane(Physics.gravity, normalVector);
            rb.AddForce(-gravityAlongSlope - normalVector, ForceMode.Acceleration);
        }
    }

    private void AirMovement() {
        Vector3 vel = rb.velocity;
        Vector3 wishDir = orientation.right * x + orientation.forward * y;
        wishDir.y = 0f;

        if (wishDir.sqrMagnitude > 0.001f) {
            wishDir = wishDir.normalized;
        } else {
            return;
        }
        
        float wishSpeed = Mathf.Min(maxAirSpeed, airAcceleration); 
        
        float currentSpeed = Vector3.Dot(vel, wishDir);
        float addSpeed = wishSpeed - currentSpeed;

        if (addSpeed <= 0f) {
            return;
        }

        float accelSpeed = Mathf.Min(airAcceleration * NetworkSettings.tickTime, addSpeed);
        rb.AddForce(wishDir * accelSpeed, ForceMode.VelocityChange);
    }

    private void OnLanding() {
        LandBob();
        ResetWallRun();

        float speed = new Vector3(rb.velocity.x, 0f, rb.velocity.z).magnitude;

        if (airSlide) {
            airSlide = false;
            sliding = true;

            if (speed <= slideBoost) {
                Slide();
            }
        }
    }

    private void LandBob() {
        float fallSpeed = Mathf.Abs(lastFallSpeed);
        if (fallSpeed > 10f) {
            LocalPlayer.Instance.playerCamera.BobOnce(Vector3.down * fallSpeed * 0.5f);
            LocalPlayer.Instance.playerCamera.BobRotOnce(Vector3.right * fallSpeed * 0.15f);

            MoveWeapon.Instance.BobPos(Vector3.up * fallSpeed * 0.15f);
            MoveWeapon.Instance.BobRot(Vector3.left * fallSpeed * 2f);
        }
    }

    private void Jump() {
        if (wallRunning || startingWallRun) {
            WallKick();
            return;
        }

        if (!grounded) {
            return;
        }

        jumpedThisFrame = true;

        float normalVel = Vector3.Dot(rb.velocity, normalVector);
        if (normalVel != 0f) {
            rb.AddForce(-normalVector * normalVel, ForceMode.VelocityChange);
        }

        LocalPlayer.Instance.playerCamera.BobRotOnce(Vector3.right * 3f);
        MoveWeapon.Instance.BobPos(Vector3.down * 0.1f);
        MoveWeapon.Instance.BobRot(Vector3.left * 8f);

        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    }

    private void StartCrouch() {
        float groundVel = new Vector3(rb.velocity.x, 0, rb.velocity.z).magnitude;

        if (groundVel > slideMinSpeed && grounded) {
            Slide();
        }
        else if (!grounded) {
            airSlide = true;
        }
    }

    private void StopCrouch() {
        airSlide = false;
    }

    private void UpdateCrouchScale() {
        float targetCrouch = crouching ? 1f : 0f;

        if (Mathf.Approximately(crouchAmount, targetCrouch)) {
            return;
        }

        float prevCrouch = crouchAmount;
        crouchAmount = Mathf.MoveTowards(crouchAmount, targetCrouch, crouchTransitionSpeed * NetworkSettings.tickTime);

        Vector3 prevScale = Vector3.Lerp(baseScale, crouchScale, prevCrouch);
        Vector3 newScale = Vector3.Lerp(baseScale, crouchScale, crouchAmount);

        transform.localScale = newScale;

        if (grounded) {
            float heightDelta = prevScale.y - newScale.y;
            transform.localPosition += Vector3.down * heightDelta;
        }
    }

    private void Slide() {
        Vector3 slopeVel = Vector3.ProjectOnPlane(rb.velocity, normalVector);
        float speed = slopeVel.magnitude;

        if (speed < 0.01f || slideCooldownActive) {
            return;
        }

        sliding = true;
        slideCooldownActive = true;
        TickInvoker.Cancel(cancelSlideCooldownAction);
        cancelSlideCooldownAction = TickInvoker.Invoke(EndSlideCooldown, TickUtil.SecondsToTick(slideCooldown));

        if (y >= 0f) {
            Vector3 dir = slopeVel / speed;
            float add = Mathf.Clamp(maxSlideSpeed - speed, 0f, slideBoost);
            rb.AddForce(dir * add, ForceMode.VelocityChange);
        }
    }

    private void EndSlideCooldown() {
        slideCooldownActive = false;
    }

    private void SlideFriction() {
        Vector3 gravityAlongSlope = Vector3.ProjectOnPlane(Physics.gravity, normalVector);
        rb.AddForce(gravityAlongSlope * slideSlopeAccel, ForceMode.Acceleration);

        Vector3 slopeVel = Vector3.ProjectOnPlane(rb.velocity, normalVector);
        float speed = slopeVel.magnitude;

        if (speed < 0.01f) {
            return;
        }

        float control = Mathf.Max(speed, slideStopSpeed);
        float drop = control * slideFriction * NetworkSettings.tickTime;
        float newSpeed = Mathf.Max(speed - drop, 0f);

        float speedDelta = speed - newSpeed;
        rb.AddForce(-slopeVel.normalized * speedDelta, ForceMode.VelocityChange);
    }

    private void StartWallRun() {
        startingWallRun = true;
        // cancelWallRunAction = player.invoker.Invoke(ForceExitWallRun, TickUtil.SecondsToTick(wallRunMaxTime));

        Vector3 flatVel = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        rb.velocity = flatVel;

        if (!jumping) {
            rb.AddForce(Vector3.up * initialWallBoost, ForceMode.Impulse);
        }
    }

    private void WallRunning() {
        Vector3 flatVel = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        float currentSpeed = flatVel.magnitude;

        Vector3 rawForwardFlat = Vector3.ProjectOnPlane(orientation.forward, Vector3.up);
        if (rawForwardFlat.sqrMagnitude > 0.001f) {
            rawForwardFlat = rawForwardFlat.normalized;
        }

        Vector3 camWallDir = Vector3.ProjectOnPlane(orientation.forward, wallNormalVector);
        camWallDir = Vector3.ProjectOnPlane(camWallDir, Vector3.up);

        Vector3 tangentDir = camWallDir.sqrMagnitude > 0.001f
            ? camWallDir.normalized
            : (flatVel.sqrMagnitude > 0.001f ? flatVel.normalized : rawForwardFlat);

        float awayAngle = Vector3.Angle(orientation.forward, wallNormalVector);
        float stickAmount = Mathf.Clamp01(Mathf.InverseLerp(20, 50, awayAngle));
        Vector3 flatDir = Vector3.Lerp(rawForwardFlat, tangentDir, stickAmount);

        if (currentSpeed < wallRunSpeed && y > threshold) {
            float speedDelta = wallRunSpeed - currentSpeed;
            float accel = Mathf.Min(speedDelta, wallRunAcceleration * NetworkSettings.tickTime);
            rb.AddForce(flatDir * accel);
        }
        else if (currentSpeed > wallRunSpeed) {
            // Brake excess horizontal speed
            float overspeed = currentSpeed - wallRunSpeed;
            rb.AddForce(-flatVel.normalized * overspeed * 0.1f * NetworkSettings.tickTime, ForceMode.VelocityChange);
        }

        if (!jumping) {
            float targetFall = IsPressingTowardWall(wallNormalVector) ? wallRunMaxFallSpeed : wallRunIdleMaxFallSpeed;

            float currentFall = rb.velocity.y;
            if (currentFall < targetFall) {
                rb.AddForce(Vector3.up * (targetFall - currentFall), ForceMode.VelocityChange);
            }
        }
    }

    private void WallKick() {
        Vector3 impulse = wallNormalVector * wallKickImpulse + Vector3.up * jumpForce;

        rb.velocity = new Vector3(rb.velocity.x, Mathf.Min(rb.velocity.y, 0f), rb.velocity.z);
        rb.AddForce(impulse, ForceMode.Impulse);

        ResetWallRun();
    }


    private void ForceExitWallRun() {
        // Cancel downward velocity before forcing exit
        float downVel = Mathf.Min(rb.velocity.y, 0f);
        if (downVel < 0f) {
            rb.AddForce(Vector3.up * -downVel, ForceMode.VelocityChange);
        }

        rb.AddForce(wallNormalVector * wallKickImpulse * 2f, ForceMode.Impulse);
        ResetWallRun();
    }

    private void ExitWallRunning() {
        if (!IsTouchingWall(-wallNormalVector, out RaycastHit hit)) {
            ResetWallRun();
            return;
        }

        if (Vector3.Angle(hit.normal, wallNormalVector) > 45f) {
            ResetWallRun();
            return;
        }

        wallNormalVector = hit.normal;
    }

    private void ResetWallRun() {
        wallRunning = false;
        startingWallRun = false;
        preWallRunning = false;
        rb.useGravity = true;
        TickInvoker.Cancel(cancelWallRunAction);
    }

    private void CheckGrounded() {
        Vector3 scale = transform.lossyScale;
        float worldRadius = playerCollider.radius * Mathf.Max(scale.x, scale.z) * 0.9f;
        float worldHeight = playerCollider.height * scale.y;
        Vector3 center = transform.position + Vector3.Scale(playerCollider.center, scale);

        float maxDistance = worldHeight * 0.5f - worldRadius + 0.2f;

        bool didHit = Physics.SphereCast(center, worldRadius, Vector3.down, out RaycastHit hit, maxDistance,
            whatIsGround, QueryTriggerInteraction.Ignore);

        grounded = didHit && Vector3.Angle(hit.normal, Vector3.up) < maxSlopeAngle;
        normalVector = grounded ? hit.normal : Vector3.up;
    }

    public void CheckWalls() {
        if (grounded || wallRunning) {
            return;
        }

        Vector3 origin = transform.position;
        Vector3[] dirs = {
            transform.right,
            -transform.right,
            transform.forward,
            -transform.forward
        };

        if (TryFindWall(origin, dirs, wallRunDistance, out RaycastHit hit)) {
            Bounds bounds = playerCollider.bounds;

            wallNormalVector = hit.normal;
            Vector3 flatVel = Vector3.ProjectOnPlane(rb.velocity, normalVector);

            if (flatVel.magnitude > 1f) {
                if (hit.distance > bounds.extents.x + 0.1f && !startingWallRun && !wallRunning) {
                    preWallRunning = true;
                }
                else if (hit.distance <= bounds.extents.x + 0.1f) {
                    StartWallRun();
                }
            }
        }
    }

    private bool TryFindWall(Vector3 origin, Vector3[] directions, float distance, out RaycastHit hit) {
        foreach (Vector3 dir in directions) {
            if (Physics.Raycast(origin, dir, out hit, distance, whatIsGround) && IsWall(hit.normal)) {
                return true;
            }
        }

        hit = default;
        return false;
    }

    private void FindWallRunRotation() {
        if (preWallRunning || startingWallRun || wallRunning) {
            float cameraAngle = LocalPlayer.Instance.playerCamera.GetCameraRot().y;
            float wallAngle = Vector3.SignedAngle(Vector3.forward, wallNormalVector, Vector3.up);
            wallRunRotation = (-Mathf.DeltaAngle(cameraAngle, wallAngle) / 90f) * wallRunCameraTilt;
        }
        else {
            wallRunRotation = 0f;
        }
    }

    private void CounterMovement(Vector2 mag, Vector3 slopeRight, Vector3 slopeForward) {
        if (jumpedThisFrame) {
            return;
        }

        Vector3 counterForce = Vector3.zero;

        if (ShouldCounter(mag.x, x)) counterForce += -slopeRight * mag.x;
        if (ShouldCounter(mag.y, y)) counterForce += -slopeForward * mag.y;

        rb.AddForce(counterForce * acceleration * counterMovement * NetworkSettings.tickTime);

        if (!sliding) {
            Vector3 flatVel = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
            if (flatVel.sqrMagnitude < 0.05f * 0.05f) {
                rb.AddForce(new Vector3(-rb.velocity.x, 0f, -rb.velocity.z), ForceMode.VelocityChange);
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
        if (sliding) {
            return;
        }

        Vector3 vel = rb.velocity;
        Vector3 flatVel = new Vector3(vel.x, 0f, vel.z);

        if (flatVel.sqrMagnitude > maxSpeed * maxSpeed) {
            // Correct excess speed via VelocityChange impulse
            Vector3 limited = flatVel.normalized * maxSpeed;
            rb.AddForce(limited - flatVel, ForceMode.VelocityChange);
        }
    }

    private bool IsTouchingWall(Vector3 direction) {
        return IsTouchingWall(direction, out _);
    }

    private bool IsTouchingWall(Vector3 direction, out RaycastHit hit) {
        Bounds b = playerCollider.bounds;
        return Physics.Raycast(b.center, direction.normalized, out hit, b.extents.x + 0.1f, whatIsGround,
                   QueryTriggerInteraction.Ignore)
               && IsWall(hit.normal);
    }

    private bool IsPressingTowardWall(Vector3 wallNormal) {
        Vector3 inputDir = orientation.right * x + orientation.forward * y;
        return Vector3.Dot(inputDir, -wallNormal) > 0f;
    }

    private Vector2 FindVelRelativeToLook() {
        float lookAngle = orientation.eulerAngles.y;
        float moveAngle = Mathf.Atan2(rb.velocity.x, rb.velocity.z) * Mathf.Rad2Deg;
        float u = Mathf.DeltaAngle(lookAngle, moveAngle);
        float v = 90f - u;
        float mag = new Vector2(rb.velocity.x, rb.velocity.z).magnitude;

        return new Vector2(mag * Mathf.Cos(v * Mathf.Deg2Rad), mag * Mathf.Cos(u * Mathf.Deg2Rad));
    }

    private bool IsWall(Vector3 v) {
        float angle = Vector3.Angle(Vector3.up, v);
        return angle >= 80f && angle <= 100f;
    }

    public Rigidbody GetRb() {
        return rb;
    }

    public float GetSpeed() {
        return rb.velocity.magnitude;
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
        return wallRunning || startingWallRun || preWallRunning;
    }

    public bool IsCrouching() {
        return crouching;
    }

    public bool IsSliding() {
        return sliding;
    }
}