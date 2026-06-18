using UnityEngine;

/// <summary>
/// Kinematic physics controller for the player billiard ball.
/// Handles velocity, spin (sidespin and topspin), Magnus effect, damping, and
/// sphere-cast collision detection. Delegates collision resolution to BallCollisionResolver.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class BilliardBall : MonoBehaviour
{
    #region Inspector Fields

    [Header("Settings")]
    public float curveStrength          = 50.0f;
    public float spinDecayRate          = 0.5f;
    public float stopVelocityThreshold  = 0.1f;

    [Header("Spin Settings")]
    public float spinStrength  = 15f;
    public float topSpinEffect = 8f;

    [Header("Kinematic Physics")]
    [SerializeField] private float linearDamping  = 0.95f;
    [SerializeField] private float angularDamping = 0.98f;
    [SerializeField] private float restitution    = 0.8f;
    [SerializeField] private float ballRadius     = 0.5f;
    [SerializeField] private LayerMask collisionMask = ~0;

    [Header("Debug Info")]
    [SerializeField] private float   debugCurrentSideSpin = 0f;
    [SerializeField] private Vector2 debugCurrentSpin     = Vector2.zero;
    [SerializeField] private Vector3 debugCurrentVelocity = Vector3.zero;

    [Header("Audio")]
    [Tooltip("Sound to play when this ball collides")]
    [SerializeField] private AudioClip collisionClip;
    [Tooltip("Base volume for collision sound (0..1)")]
    [SerializeField] [Range(0f, 1f)] private float collisionVolume = 1f;
    [Tooltip("Divide speed by this value to compute scaled volume")]
    [SerializeField] private float collisionSpeedScale = 8f;

    #endregion

    #region Public State

    public float currentSideSpin = 0f;
    public bool oneHitKillActive = false;

    #endregion

    #region Private State

    private Vector2 currentSpin           = Vector2.zero;
    private Vector3 currentVelocity       = Vector3.zero;
    private Vector3 currentAngularVelocity = Vector3.zero;
    private bool    wasMovingLastFrame    = false;

    private Rigidbody            rb;
    private BallCollisionResolver collisionResolver;

    private AudioSource audioSource;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic            = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        var col = GetComponent<SphereCollider>();
        if (col != null)
            ballRadius = col.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);

        collisionResolver = new BallCollisionResolver(restitution, ballRadius);

        // Setup AudioSource (minimal, created if missing)
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        // Try to route to GameManager SFX mixer group if available
        if (GameManager.Instance != null && AudioManager.Instance.sfxMixerGroup != null)
            audioSource.outputAudioMixerGroup = AudioManager.Instance.sfxMixerGroup;
    }

    private void FixedUpdate()
    {
        if (currentVelocity.magnitude > stopVelocityThreshold)
        {
            if (currentSpin.magnitude > 0.01f)
            {
                ApplySpinEffects();
                DecaySpin();
            }

            ApplyDamping();
            MoveWithCollision();
        }
        else
        {
            ApplyDamping();
        }

        UpdateRotation();
        UpdateDebugValues();
    }

    // Fallback for any edge cases SphereCast misses
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.contactCount == 0) return;
        ContactPoint contact = collision.GetContact(0);
        HandleWallCollision(contact.normal, contact.point);
    }

    #endregion

    #region Public API – Force & Spin

    public void Initialize(Rigidbody rigidbody)
    {
        rb = rigidbody;
    }

    /// <summary>Fully resets all velocity, spin, and angular state to zero.</summary>
    public void ResetState()
    {
        currentVelocity        = Vector3.zero;
        currentAngularVelocity = Vector3.zero;
        currentSideSpin        = 0f;
        currentSpin            = Vector2.zero;
        wasMovingLastFrame     = false;
        UpdateDebugValues();
    }

    /// <summary>Fires the ball with a direction, power and optional side spin.</summary>
    public void Shoot(Vector3 direction, float power, float sideSpin = 0f)
    {
        currentVelocity  = direction.normalized * power;
        currentSideSpin  = sideSpin;
        currentSpin      = new Vector2(sideSpin, 0f);
        ApplyAngularVelocity(currentSpin);
        UpdateDebugValues();
    }

    public void ApplyForce(Vector3 force)
    {
        currentVelocity += force / rb.mass;
        if (currentSpin.magnitude > 0.01f)
            ApplyAngularVelocity(currentSpin);
        UpdateDebugValues();
    }

    public void ApplyForce(Vector3 force, Vector2 spin)
    {
        currentVelocity += force / rb.mass;
        SetSpin(spin);
    }

    public void ApplyForceAndResetSpin(Vector3 force)
    {
        currentVelocity        += force / rb.mass;
        currentSideSpin         = 0f;
        currentSpin             = Vector2.zero;
        currentAngularVelocity  = Vector3.zero;
        UpdateDebugValues();
    }

    /// <summary>Applies force and sets spin from a normalized curve intensity.</summary>
    public void ApplyForceWithCurve(Vector3 force, float curveIntensity)
    {
        currentVelocity += force / rb.mass;
        float sideSpin   = Mathf.Clamp(curveIntensity / 2.0f, -1f, 1f);
        currentSideSpin  = sideSpin;
        currentSpin      = new Vector2(sideSpin, currentSpin.y);
        ApplyAngularVelocity(currentSpin);
        UpdateDebugValues();
    }

    /// <summary>Applies force with a lateral direction determining spin sign.</summary>
    public void ApplyForceWithCurve(Vector3 force, Vector3 lateralDirection, float lateralIntensity)
    {
        currentVelocity += force / rb.mass;
        float sign       = lateralDirection != Vector3.zero
                           ? Mathf.Sign(Vector3.Dot(lateralDirection.normalized, Vector3.right))
                           : 0f;
        float sideSpin   = Mathf.Clamp((lateralIntensity / 2.0f) * sign, -1f, 1f);
        currentSideSpin  = sideSpin;
        currentSpin      = new Vector2(sideSpin, currentSpin.y);
        ApplyAngularVelocity(currentSpin);
        UpdateDebugValues();
    }

    /// <summary>Applies force with explicit 2D spin; falls back to curveIntensity if spin is negligible.</summary>
    public void ApplyForceWithCurve(Vector3 force, float curveIntensity, Vector2 spin)
    {
        currentVelocity += force / rb.mass;
        SetSpin(spin);

        if (spin.magnitude < 0.01f && Mathf.Abs(curveIntensity) > 0.01f)
        {
            float legacySideSpin = Mathf.Clamp(curveIntensity / 2.0f, -1f, 1f);
            currentSideSpin = legacySideSpin;
            currentSpin     = new Vector2(legacySideSpin, currentSpin.y);
        }

        UpdateDebugValues();
    }

    public bool IsBallMoving()
    {
        bool isMoving = currentVelocity.magnitude > stopVelocityThreshold ||
                        currentAngularVelocity.magnitude > stopVelocityThreshold;

        if (!isMoving && wasMovingLastFrame)
        {
            currentVelocity        = Vector3.zero;
            currentAngularVelocity = Vector3.zero;
            currentSideSpin        = 0f;
            currentSpin            = Vector2.zero;
            UpdateDebugValues();
        }

        wasMovingLastFrame = isMoving;
        return isMoving;
    }

    public Vector3 Velocity => currentVelocity;

    #endregion

    #region Movement & Collision

    private void MoveWithCollision()
    {
        Vector3 moveStep     = currentVelocity * Time.fixedDeltaTime;
        float   moveDistance = moveStep.magnitude;

        if (moveDistance < 0.0001f) return;

        bool hit = Physics.SphereCast(
            rb.position,
            ballRadius * 0.99f,
            moveStep.normalized,
            out RaycastHit hitInfo,
            moveDistance + ballRadius * 0.1f,
            collisionMask,
            QueryTriggerInteraction.Ignore);

        if (hit)
            HandleSweepCollision(hitInfo);
        else
            rb.MovePosition(rb.position + moveStep);
    }

    private void HandleSweepCollision(RaycastHit hitInfo)
    {
        var otherBall  = hitInfo.rigidbody?.GetComponent<BilliardBall>();
        var enemyBall  = hitInfo.rigidbody?.GetComponent<EnemyBallBase>();

        if (otherBall != null)
            HandleBallToBallCollision(otherBall, hitInfo.normal);
        else if (enemyBall != null)
            HandleBallToEnemyCollision(enemyBall, hitInfo.normal);
        else
            HandleWallCollision(hitInfo.normal, hitInfo.point);
    }

    private void HandleWallCollision(Vector3 normal, Vector3 hitPoint)
    {
        currentVelocity = collisionResolver.ResolveWall(
            rb, currentVelocity, currentSpin, normal, hitPoint,
            out Vector2 newSpin, out Vector3 angularScale);

        currentSpin             = newSpin;
        currentSideSpin         = currentSpin.x;
        currentAngularVelocity  *= angularScale.x;

        // Play collision sound (scale volume by speed)
        PlayCollisionSound(currentVelocity.magnitude);
    }

    private void HandleBallToBallCollision(BilliardBall other, Vector3 normal)
    {
        collisionResolver.ResolveBallToBall(
            rb, other, currentVelocity, other.currentVelocity, normal,
            out Vector3 myNew, out Vector3 otherNew);

        // compute approximate impact strength and play sound
        float impactStrength = Mathf.Clamp01((currentVelocity.magnitude + other.currentVelocity.magnitude) / (collisionSpeedScale * 2f));
        PlayCollisionSound(impactStrength);

        currentVelocity       = myNew;
        other.currentVelocity = otherNew;
    }

    private void HandleBallToEnemyCollision(EnemyBallBase enemy, Vector3 normal)
    {
        if (oneHitKillActive)
        {
            PlayCollisionSound(currentVelocity.magnitude / collisionSpeedScale);
            Destroy(enemy.gameObject);
            oneHitKillActive = false;
            return;
        }
        // play sound based on speed
        PlayCollisionSound(currentVelocity.magnitude / collisionSpeedScale);
        currentVelocity = collisionResolver.ResolveBallToEnemy(rb, enemy, currentVelocity, normal);
    }

    #endregion

    #region Spin & Physics

    private void SetSpin(Vector2 spin)
    {
        currentSpin     = spin;
        currentSideSpin = spin.x;
        ApplyAngularVelocity(spin);
        UpdateDebugValues();
    }

    private void ApplyAngularVelocity(Vector2 spin)
    {
        currentAngularVelocity += new Vector3(-spin.y, 0, spin.x) * spinStrength;
    }

    private void ApplySpinEffects()
    {
        if (Mathf.Abs(currentSpin.x) > 0.01f)
            ApplyMagnusEffect();

        if (Mathf.Abs(currentSpin.y) > 0.01f)
            ApplyTopSpinEffect();
    }

    private void ApplyMagnusEffect()
    {
        if (currentVelocity.magnitude < 0.01f) return;

        Vector3 perp          = Vector3.Cross(currentVelocity.normalized, Vector3.forward).normalized;
        float   velMul        = Mathf.Clamp(currentVelocity.magnitude, 1.0f, 12f);
        float   magnusAcc     = -currentSpin.x * curveStrength * velMul;
        currentVelocity      += perp * magnusAcc * Time.fixedDeltaTime;

        Debug.DrawRay(transform.position, perp * magnusAcc * 0.1f, Color.green, 0.1f);
    }

    private void ApplyTopSpinEffect()
    {
        if (currentVelocity.magnitude < 0.01f) return;
        currentVelocity += currentVelocity.normalized * (currentSpin.y * topSpinEffect * Time.fixedDeltaTime);
    }

    private void ApplyDamping()
    {
        float linearFactor  = Mathf.Pow(linearDamping,  Time.fixedDeltaTime * 60f);
        float angularFactor = Mathf.Pow(angularDamping, Time.fixedDeltaTime * 60f);

        currentVelocity        *= linearFactor;
        currentAngularVelocity *= angularFactor;

        if (currentVelocity.magnitude        < stopVelocityThreshold) currentVelocity        = Vector3.zero;
        if (currentAngularVelocity.magnitude < stopVelocityThreshold) currentAngularVelocity = Vector3.zero;
    }

    private void DecaySpin()
    {
        currentSpin     = Vector2.MoveTowards(currentSpin, Vector2.zero, spinDecayRate * Time.fixedDeltaTime);
        currentSideSpin = currentSpin.x;
    }

    private void UpdateRotation()
    {
        if (currentAngularVelocity.magnitude > 0.01f)
        {
            Quaternion delta = Quaternion.Euler(currentAngularVelocity * Mathf.Rad2Deg * Time.fixedDeltaTime);
            rb.MoveRotation(rb.rotation * delta);
        }
    }

    #endregion

    #region Audio Helpers

    private void PlayCollisionSound(float speedBasedValue)
    {
        if (collisionClip == null || audioSource == null) return;

        // If speedBasedValue is a magnitude, normalize it; otherwise treat as already normalized 0..1
        float normalized = Mathf.Abs(speedBasedValue);
        if (normalized > 1f)
            normalized = Mathf.Clamp01(normalized / collisionSpeedScale);

        audioSource.PlayOneShot(collisionClip, Mathf.Clamp01(collisionVolume * normalized));
    }

    #endregion

    #region Debug

    private void UpdateDebugValues()
    {
        debugCurrentSideSpin = currentSideSpin;
        debugCurrentSpin     = currentSpin;
        debugCurrentVelocity = currentVelocity;
    }

    #endregion
}