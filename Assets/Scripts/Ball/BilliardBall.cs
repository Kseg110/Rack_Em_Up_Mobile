using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BilliardBall : MonoBehaviour
{
    [Header("Settings")]
    public float curveStrength = 15.0f;
    public float spinDecayRate = 0.5f;
    public float stopVelocityThreshold = 0.1f;

    [Header("Spin Settings")]
    public float spinStrength = 15f;
    public float topSpinEffect = 8f;

    [Header("Kinematic Physics")]
    [SerializeField] private float linearDamping = 0.95f;
    [SerializeField] private float angularDamping = 0.98f;
    [SerializeField] private float restitution = 0.8f;
    [SerializeField] private float ballRadius = 0.5f;
    [SerializeField] private LayerMask collisionMask = ~0; // All layers by default

    [Header("State - Debug Info")]
    [SerializeField] private float debugCurrentSideSpin = 0f;
    [SerializeField] private Vector2 debugCurrentSpin = Vector2.zero;
    [SerializeField] private Vector3 debugCurrentVelocity = Vector3.zero;

    public float currentSideSpin = 0f;
    private Vector2 currentSpin = Vector2.zero;

    private Rigidbody rb;
    private bool wasMovingLastFrame = false;

    private Vector3 currentVelocity = Vector3.zero;
    private Vector3 currentAngularVelocity = Vector3.zero;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // Auto-detect radius from collider
        var col = GetComponent<SphereCollider>();
        if (col != null)
            ballRadius = col.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
    }

    void Start()
    {
        Debug.Log($"[BilliardBall] Initialized in KINEMATIC mode. Radius={ballRadius:F2}");
    }

    public void Initialize(Rigidbody rigidbody, MonoBehaviour ownerMonoBehaviour = null)
    {
        rb = rigidbody;
    }

    public void Shoot(Vector3 direction, float power, float sideSpin = 0f)
    {
        currentVelocity = direction.normalized * power;
        currentSideSpin = sideSpin;
        currentSpin = new Vector2(sideSpin, 0f);
        ApplyAngularVelocity(currentSpin);
        UpdateDebugValues();
        Debug.Log($"[BilliardBall] Kinematic shot: Velocity={currentVelocity.magnitude:F2}, Spin={sideSpin}");
    }

    public void ApplyForce(Vector3 force)
    {
        currentVelocity += force / rb.mass;
        if (currentSpin.magnitude > 0.01f)
            ApplyAngularVelocity(currentSpin);
        UpdateDebugValues();
    }

    public void ApplyForceWithCurve(Vector3 baseForce, float curveIntensity)
    {
        currentVelocity += baseForce / rb.mass;
        float sideSpin = Mathf.Clamp(curveIntensity / 2.0f, -1f, 1f);
        currentSideSpin = sideSpin;
        currentSpin = new Vector2(sideSpin, currentSpin.y);
        ApplyAngularVelocity(currentSpin);
        UpdateDebugValues();
    }

    public void ApplyForceWithCurve(Vector3 baseForce, Vector3 lateralDirection, float lateralIntensity)
    {
        currentVelocity += baseForce / rb.mass;
        float sign = lateralDirection != Vector3.zero
            ? Mathf.Sign(Vector3.Dot(lateralDirection.normalized, Vector3.right))
            : 0f;
        float sideSpin = Mathf.Clamp((lateralIntensity / 2.0f) * sign, -1f, 1f);
        currentSideSpin = sideSpin;
        currentSpin = new Vector2(sideSpin, currentSpin.y);
        ApplyAngularVelocity(currentSpin);
        UpdateDebugValues();
    }

    public void ApplyForce(Vector3 force, Vector2 spin)
    {
        currentVelocity += force / rb.mass;
        ApplySpin(spin);
        UpdateDebugValues();
    }

    public void ApplyForceWithCurve(Vector3 force, float curveIntensity, Vector2 spin)
    {
        currentVelocity += force / rb.mass;
        ApplySpin(spin);
        if (spin.magnitude < 0.01f && Mathf.Abs(curveIntensity) > 0.01f)
        {
            float legacySideSpin = Mathf.Clamp(curveIntensity / 2.0f, -1f, 1f);
            currentSideSpin = legacySideSpin;
            currentSpin = new Vector2(legacySideSpin, currentSpin.y);
        }
        UpdateDebugValues();
    }

    public void ApplyForceAndResetSpin(Vector3 force)
    {
        currentVelocity += force / rb.mass;
        currentSideSpin = 0f;
        currentSpin = Vector2.zero;
        currentAngularVelocity = Vector3.zero;
        UpdateDebugValues();
    }

    private void ApplySpin(Vector2 spin)
    {
        currentSpin = spin;
        currentSideSpin = spin.x;
        ApplyAngularVelocity(spin);
        UpdateDebugValues();
    }

    private void ApplyAngularVelocity(Vector2 spin)
    {
        currentAngularVelocity += new Vector3(-spin.y, 0, spin.x) * spinStrength;
    }

    public bool IsBallMoving()
    {
        bool isLinearVelocityLow = currentVelocity.magnitude <= stopVelocityThreshold;
        bool isAngularVelocityLow = currentAngularVelocity.magnitude <= stopVelocityThreshold;
        bool isMoving = !(isLinearVelocityLow && isAngularVelocityLow);

        if (!isMoving && wasMovingLastFrame)
        {
            currentVelocity = Vector3.zero;
            currentAngularVelocity = Vector3.zero;
            currentSideSpin = 0f;  
            currentSpin = Vector2.zero;
            UpdateDebugValues();
            Debug.Log("[BilliardBall] Ball stopped");
        }

        wasMovingLastFrame = isMoving;
        return isMoving;
    }

    void FixedUpdate()
    {
        UpdateDebugValues();

        if (currentVelocity.magnitude > stopVelocityThreshold)
        {
            if (currentSpin.magnitude > 0.01f)
            {
                ApplySpinEffectsKinematic();
                DecaySpin();
            }

            ApplyDamping();
            MoveWithCollision();
        }
        else
        {
            ApplyDamping(); // Still damp angular
        }

        UpdateRotation();
    }

    /// <summary>
    /// Sweeps a sphere along the intended movement vector, resolves collisions
    /// and reflects velocity off any surface hit before calling rb.MovePosition.
    /// </summary>
    private void MoveWithCollision()
    {
        Vector3 moveStep = currentVelocity * Time.fixedDeltaTime;
        float moveDistance = moveStep.magnitude;

        if (moveDistance < 0.0001f) return;

        // SphereCast from current position along move direction
        bool hit = Physics.SphereCast(
            rb.position,
            ballRadius * 0.99f,     // Slightly smaller to avoid starting inside collider
            moveStep.normalized,
            out RaycastHit hitInfo,
            moveDistance + ballRadius * 0.1f,
            collisionMask,
            QueryTriggerInteraction.Ignore
        );

        if (hit)
        {
            HandleSweepCollision(hitInfo);
        }
        else
        {
            rb.MovePosition(rb.position + moveStep);
        }
    }

    private void HandleSweepCollision(RaycastHit hitInfo)
    {
        Vector3 normal = hitInfo.normal;

        // Check if this is another kinematic BilliardBall
        var otherBall = hitInfo.rigidbody?.GetComponent<BilliardBall>();
        if (otherBall != null)
        {
            ResolveBallToBallCollision(otherBall, normal);
            return;
        }

        // Check if this is a dynamic enemy ball
        var enemyBall = hitInfo.rigidbody?.GetComponent<EnemyBallBase>();
        if (enemyBall != null)
        {
            ResolveBallToEnemyCollision(enemyBall, normal);
            return;
        }

        // Wall / obstacle reflection
        ResolveWallCollision(normal, hitInfo.point);
    }

    private void ResolveWallCollision(Vector3 normal, Vector3 hitPoint)
    {
        // Reflect velocity off the surface
        currentVelocity = Vector3.Reflect(currentVelocity, normal) * restitution;

        // Reduce spin on wall impact
        currentSpin *= 0.9f;
        currentSideSpin = currentSpin.x;
        currentAngularVelocity *= 0.9f;

        // Push slightly away from wall to prevent sticking
        rb.MovePosition(hitPoint + normal * (ballRadius + 0.001f));

        Debug.Log($"[BilliardBall] Wall collision: Normal={normal}, NewVelocity={currentVelocity.magnitude:F2}");
        Debug.DrawRay(hitPoint, normal * 0.5f, Color.red, 0.2f);
    }

    private void ResolveBallToBallCollision(BilliardBall otherBall, Vector3 normal)
    {
        // Elastic-like collision between two kinematic balls
        Vector3 myVelocity = currentVelocity;
        Vector3 otherVelocity = otherBall.currentVelocity;

        // Project velocities along the collision normal
        float mySpeed = Vector3.Dot(myVelocity, normal);
        float otherSpeed = Vector3.Dot(otherVelocity, -normal);

        // Only resolve if balls are moving toward each other
        if (mySpeed < 0) return;

        // Exchange velocity components along normal (equal mass assumption)
        Vector3 myNormalVel = normal * mySpeed;
        Vector3 otherNormalVel = -normal * otherSpeed;

        currentVelocity = (myVelocity - myNormalVel + otherNormalVel) * restitution;
        otherBall.currentVelocity = (otherVelocity - otherNormalVel + myNormalVel) * restitution;

        // Separate balls to prevent overlap
        rb.MovePosition(rb.position - normal * (ballRadius * 0.1f));

        Debug.Log($"[BilliardBall] Ball-to-ball collision: TransferredVelocity={otherBall.currentVelocity.magnitude:F2}");
    }

    private void ResolveBallToEnemyCollision(EnemyBallBase enemyBall, Vector3 normal)
    {
        // Transfer momentum to the dynamic enemy ball
        Vector3 impulse = currentVelocity * rb.mass * restitution;
        enemyBall.ApplyForce(impulse);

        // Reflect our velocity
        currentVelocity = Vector3.Reflect(currentVelocity, normal) * restitution * 0.5f;

        Debug.Log($"[BilliardBall] Ball-to-enemy collision: Impulse={impulse.magnitude:F2}");
    }

    private void UpdateRotation()
    {
        if (currentAngularVelocity.magnitude > 0.01f)
        {
            Quaternion deltaRotation = Quaternion.Euler(currentAngularVelocity * Mathf.Rad2Deg * Time.fixedDeltaTime);
            rb.MoveRotation(rb.rotation * deltaRotation);
        }
    }

    private void ApplySpinEffectsKinematic()
    {
        if (Mathf.Abs(currentSpin.x) > 0.01f)
            ApplyMagnusEffectKinematic();

        if (Mathf.Abs(currentSpin.y) > 0.01f)
            ApplyTopSpinEffectKinematic();
    }

    private void ApplyMagnusEffectKinematic()
    {
        if (currentVelocity.magnitude < 0.01f) return;

        Vector3 perpDirection = Vector3.Cross(currentVelocity.normalized, Vector3.forward).normalized;
        float velocityMultiplier = Mathf.Clamp(currentVelocity.magnitude, 1.0f, 12f);
        float magnusAcceleration = -currentSpin.x * curveStrength * velocityMultiplier;
        currentVelocity += perpDirection * magnusAcceleration * Time.fixedDeltaTime;

        Debug.DrawRay(transform.position, perpDirection * magnusAcceleration * 0.1f, Color.green, 0.1f);
    }

    private void ApplyTopSpinEffectKinematic()
    {
        if (currentVelocity.magnitude < 0.01f) return;

        currentVelocity += currentVelocity.normalized * (currentSpin.y * topSpinEffect * Time.fixedDeltaTime);
    }

    private void ApplyDamping()
    {
        float dampingFactor = Mathf.Pow(linearDamping, Time.fixedDeltaTime * 60f);
        currentVelocity *= dampingFactor;

        float angularDampingFactor = Mathf.Pow(angularDamping, Time.fixedDeltaTime * 60f);
        currentAngularVelocity *= angularDampingFactor;

        if (currentVelocity.magnitude < stopVelocityThreshold)
            currentVelocity = Vector3.zero;

        if (currentAngularVelocity.magnitude < stopVelocityThreshold)
            currentAngularVelocity = Vector3.zero;
    }

    void DecaySpin()
    {
        currentSpin = Vector2.MoveTowards(currentSpin, Vector2.zero, spinDecayRate * Time.fixedDeltaTime);
        currentSideSpin = currentSpin.x;
    }

    // Keep OnCollisionEnter as a fallback for any cases SphereCast misses
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.contactCount == 0) return;

        // This will rarely fire for kinematic, but handles edge cases
        ContactPoint contact = collision.GetContact(0);
        ResolveWallCollision(contact.normal, contact.point);

        Debug.Log($"[BilliardBall] Fallback OnCollisionEnter with: {collision.gameObject.name}");
    }

    private void UpdateDebugValues()
    {
        debugCurrentSideSpin = currentSideSpin;
        debugCurrentSpin = currentSpin;
        debugCurrentVelocity = currentVelocity;
    }

    public Vector3 Velocity => currentVelocity;
}