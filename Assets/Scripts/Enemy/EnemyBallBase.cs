using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public abstract class EnemyBallBase : MonoBehaviour
{
    [Header("Physics")]
    [SerializeField] protected RigidbodyConfig rigidbodyConfig;
    [SerializeField] protected float stopVelocityThreshold = 0.1f;
    [SerializeField] protected float mass = 1f;

    [Header("Ball Physics")]
    [SerializeField] protected float restitution    = 0.8f;
    [SerializeField] protected float linearDamping  = 0.97f;
    [SerializeField] protected float angularDamping = 0.98f;
    [SerializeField] protected float ballRadius     = 0.5f;
    [SerializeField] protected LayerMask collisionMask = ~0;

    [Header("Curve (optional simple support)")]
    [SerializeField] protected float curvePullForce = 4f;
    [SerializeField] protected float curveDuration  = 0.6f;

    [Header("Tags")]
    [SerializeField] private string enemyTag = "Enemy";

    protected Rigidbody rb;
    protected Vector3 currentVelocity = Vector3.zero;
    private bool wasMovingLastFixedUpdate;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (rigidbodyConfig != null)
            RigidbodyConfigurator.ConfigureRigidbody(rb, rigidbodyConfig);

        rb.mass = mass;

        // Make kinematic so we control movement manually, same as BilliardBall
        rb.isKinematic = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        var col = GetComponent<SphereCollider>();
        if (col != null)
            ballRadius = col.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
    }

    protected virtual void Start() { }

    protected virtual void FixedUpdate()
    {
        bool isMoving = IsBallMoving();

        if (isMoving)
        {
            ApplyDamping();
            MoveWithCollision();
        }
        else
        {
            ApplyDamping();
        }

        wasMovingLastFixedUpdate = isMoving;
    }

    public virtual void Initialize(Rigidbody rigidbody, RigidbodyConfig config = null)
    {
        rb = rigidbody ?? rb;

        if (config != null)
        {
            rigidbodyConfig = config;
            RigidbodyConfigurator.ConfigureRigidbody(rb, rigidbodyConfig);
        }

        rb.mass = mass;
    }

    public virtual bool IsBallMoving()
    {
        bool isMoving = currentVelocity.magnitude > stopVelocityThreshold;

        if (!isMoving && wasMovingLastFixedUpdate)
        {
            currentVelocity = Vector3.zero;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        return isMoving;
    }

    public virtual void ApplyForce(Vector3 impulse)
    {
        currentVelocity += impulse / mass;
    }

    public virtual void ApplyForceWithCurve(Vector3 baseImpulse, Vector3 lateralDirection, float lateralIntensity)
    {
        currentVelocity += baseImpulse / mass;

        if (lateralIntensity > 0.01f && lateralDirection != Vector3.zero)
        {
            StopAllCoroutines();
            StartCoroutine(ApplySimpleCurveCoroutine(lateralDirection.normalized, lateralIntensity));
        }
    }

    // --- Movement & Collision ---

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
        var otherEnemy = hitInfo.rigidbody?.GetComponent<EnemyBallBase>();
        var playerBall = hitInfo.rigidbody?.GetComponent<BilliardBall>();

        if (playerBall != null)
        {
            // Let the player ball's resolver handle this; just reflect
            currentVelocity = Vector3.Reflect(currentVelocity, hitInfo.normal) * restitution;
        }
        else if (otherEnemy != null)
        {
            HandleEnemyToEnemyCollision(otherEnemy, hitInfo.normal);
        }
        else
        {
            HandleWallCollision(hitInfo.normal, hitInfo.point);
        }
    }

    private void HandleWallCollision(Vector3 normal, Vector3 hitPoint)
    {
        currentVelocity = Vector3.Reflect(currentVelocity, normal) * restitution;
        rb.MovePosition(hitPoint + normal * (ballRadius + 0.001f));

        Debug.DrawRay(hitPoint, normal * 0.5f, Color.yellow, 0.2f);
    }

    private void HandleEnemyToEnemyCollision(EnemyBallBase other, Vector3 normal)
    {
        float mySpeed    = Vector3.Dot(currentVelocity, normal);
        float otherSpeed = Vector3.Dot(other.currentVelocity, -normal);

        if (mySpeed < 0f) return;

        Vector3 myNormalVel    = normal * mySpeed;
        Vector3 otherNormalVel = -normal * otherSpeed;

        currentVelocity       = (currentVelocity       - myNormalVel    + otherNormalVel) * restitution;
        other.currentVelocity = (other.currentVelocity - otherNormalVel + myNormalVel)    * restitution;

        rb.MovePosition(rb.position - normal * (ballRadius * 0.1f));
    }

    private void ApplyDamping()
    {
        float factor = Mathf.Pow(linearDamping, Time.fixedDeltaTime * 60f);
        currentVelocity *= factor;

        if (currentVelocity.magnitude < stopVelocityThreshold)
            currentVelocity = Vector3.zero;
    }

    // --- Curve coroutine ---

    private IEnumerator ApplySimpleCurveCoroutine(Vector3 lateralDir, float intensity)
    {
        float elapsed = 0f;

        while (elapsed < curveDuration && currentVelocity.magnitude > stopVelocityThreshold)
        {
            float t            = 1f - (elapsed / curveDuration);
            Vector3 lateralForce = lateralDir * (intensity * curvePullForce * t) * Time.fixedDeltaTime;
            currentVelocity   += lateralForce / mass;

            Debug.DrawRay(rb.position, lateralForce * 10f, Color.magenta, 0.1f);

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }
    }

    protected virtual void OnTriggerEnter(Collider other)
    {
        if (!gameObject.CompareTag(enemyTag)) return;

        if (other.GetComponent<Pockets>() != null || other.CompareTag("Pocket"))
            Destroy(gameObject);
    }
}
