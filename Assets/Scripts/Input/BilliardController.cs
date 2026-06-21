using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Top-level player controller for the billiard cue ball.
/// Orchestrates aiming (AimingSystem), power charging (ShootButton), spin input (SpinButton),
/// shot firing (BilliardBall), trajectory preview (Projection), and all related visuals.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(LineRenderer))]
public class BilliardController : PhysicsMaterialManager
{
    #region Inspector Fields

    [Header("Power Settings")]
    [SerializeField] private float maxPower    = 200f;
    [SerializeField] private float chargeSpeed = 15f;

    [Header("Components")]
    [SerializeField] private BilliardBall  billiardBall;
    public AimingSystem                    aimingSystem;
    [SerializeField] private RigidbodyConfig  rigidbodyConfig;
    [SerializeField] private Projection    trajectoryProjection;

    [Header("Visuals")]
    [SerializeField] private Transform arrowIndicator;
    [SerializeField] private Color     minPowerColor = Color.white;
    [SerializeField] private Color     maxPowerColor = Color.red;
    [SerializeField] private Color     curveColor    = Color.yellow;

    [Header("HUD Buttons")]
    [SerializeField] private RadialPowerBar powerBar;
    [SerializeField] private ShootButton    shootButton;
    [SerializeField] private SpinButton     spinButton;

    #endregion

    #region Private State

    private Rigidbody    rb;
    private LineRenderer aimLine;
    private Camera       mainCam;

    private bool  isCharging;
    private float currentPower;
    private bool  ballWasMovingLastFrame;

    private bool oneHitKillActive = true;

    #endregion

    #region Properties

    public float PowerPercentage => currentPower / maxPower;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        rb      = GetComponent<Rigidbody>();
        aimLine = GetComponent<LineRenderer>();
        mainCam = Camera.main;

        RigidbodyConfigurator.ConfigureRigidbody(rb, rigidbodyConfig);

        if (billiardBall == null)
        {
            billiardBall = GetComponent<BilliardBall>() ?? gameObject.AddComponent<BilliardBall>();
        }

        billiardBall.Initialize(rb);
        aimingSystem.Initialize(mainCam, transform);
        SetupLineRenderer();
    }

    protected override void Start()
    {
        base.Start();
        ApplyPhysicsMaterial();
        ResolveHUDReferences();
        SetupShootButton();

        if (InputManager.Instance != null)
            InputManager.Instance.OnTouchEnd += OnTouchEnd;
    }

    private void OnDestroy()
    {
        if (shootButton != null)
        {
            shootButton.OnStartCharging -= StartPowerCharging;
            shootButton.OnFireShot      -= FireShot;
        }

        if (InputManager.Instance != null)
            InputManager.Instance.OnTouchEnd -= OnTouchEnd;
    }

    private void Update()
    {
        bool isBallMoving = billiardBall.IsBallMoving();

        if (ballWasMovingLastFrame && !isBallMoving)
            spinButton?.ResetSpin();

        ballWasMovingLastFrame = isBallMoving;

        if (isBallMoving || IsUIBlockingAim())
        {
            HideAimingVisuals();
            return;
        }

        aimingSystem.UpdateAiming();

        if (isCharging)
            HandlePowerCharging();

        UpdateVisuals();
        UpdateTrajectoryPreview();
    }

    #endregion

    #region Setup

    private void ResolveHUDReferences()
    {
        if (shootButton == null) shootButton = FindFirstObjectByType<ShootButton>();
        if (spinButton  == null) spinButton  = FindFirstObjectByType<SpinButton>();

        if (spinButton == null)
            Debug.LogWarning("[BilliardController] SpinButton not found in scene.");
    }

    private void SetupShootButton()
    {
        if (shootButton == null) return;
        shootButton.OnStartCharging += StartPowerCharging;
        shootButton.OnFireShot      += FireShot;
    }

    private void SetupLineRenderer()
    {
        aimLine.positionCount = 2;
        aimLine.startWidth    = 0.05f;
        aimLine.endWidth      = 0.05f;
        aimLine.material      = new Material(Shader.Find("Sprites/Default"));
        aimLine.startColor    = Color.red;
        aimLine.endColor      = Color.red;
    }

    private void ApplyPhysicsMaterial()
    {
        var ballCollider = GetComponent<Collider>();
        if (ballCollider != null)
            ballCollider.material = GetBallMaterial();
    }

    #endregion

    #region Input Handling

    private void OnTouchEnd() => aimingSystem.OnTouchRelease();

    private void StartPowerCharging()
    {
        if (billiardBall.IsBallMoving()) return;

        isCharging    = true;
        currentPower  = 0f;
        powerBar?.SetActive(true);
    }

    private void HandlePowerCharging()
    {
        currentPower = Mathf.Clamp(currentPower + chargeSpeed * Time.deltaTime, 0f, maxPower);
        powerBar?.UpdatePower(PowerPercentage);

        if (shootButton != null)
        {
            if (PowerPercentage >= 0.8f) shootButton.SetReadyToFireState();
            else                          shootButton.SetChargingState();
        }
    }

    private void FireShot()
    {
        if (!isCharging) return;

        Vector3 baseForce = aimingSystem.AimDirection * currentPower;
        Vector2 spin      = spinButton?.GetSpinNormalized() ?? Vector2.zero;

        if (HasSignificantSpin() || (aimingSystem.IsCurveShotActive && Mathf.Abs(aimingSystem.CurveIntensity) > 0.1f))
            billiardBall.ApplyForceWithCurve(baseForce, aimingSystem.CurveIntensity, spin);
        else
            billiardBall.ApplyForce(baseForce, spin);

        if (GameManager.Instance != null)
            GameManager.Instance.Shots--;

        ResetShotState();
    }

    // Legacy compatibility methods
    public void Shoot() => FireShot();

    public void Shoot(Vector2 velocity)
    {
        billiardBall.ApplyForceAndResetSpin(new Vector3(velocity.x, velocity.y, 0f));
        if (GameManager.Instance != null) GameManager.Instance.Shots--;
        HideAimingVisuals();
    }

    private void ResetShotState()
    {
        isCharging   = false;
        currentPower = 0f;
        powerBar?.SetActive(false);
        shootButton?.SetIdleState();
        HideAimingVisuals();
    }

    #endregion

    #region Visuals

    private void UpdateVisuals()
    {
        aimLine.enabled = true;
        arrowIndicator.gameObject.SetActive(true);

        DrawAimLine(aimingSystem.CurrentAimLineLength);

        Vector2 currentSpin    = spinButton?.GetSpinNormalized() ?? Vector2.zero;
        bool    hasSpin        = currentSpin.magnitude > 0.1f;
        float   powerPercent   = currentPower / maxPower;

        Color baseColor = aimingSystem.IsCurveShotActive ? curveColor
                        : hasSpin                        ? Color.magenta
                        : minPowerColor;

        Color lineColor = isCharging
            ? Color.Lerp(baseColor, maxPowerColor, powerPercent)
            : baseColor;

        aimLine.startColor = lineColor;
        aimLine.endColor   = lineColor;

        arrowIndicator.localScale = isCharging
            ? Vector3.one * (1f + powerPercent * 0.5f)
            : Vector3.one;
    }

    private void UpdateTrajectoryPreview()
    {
        if (trajectoryProjection == null) return;

        Vector2 currentSpin = spinButton?.GetSpinNormalized() ?? Vector2.zero;
        bool    hasSpin     = currentSpin.magnitude > 0.1f;

        if (!hasSpin)
        {
            trajectoryProjection.HideCurvePreview();
            return;
        }

        float   previewPower        = isCharging ? currentPower : maxPower * 0.5f;
        Vector3 baseVelocity        = aimingSystem.AimDirection * previewPower;
        Vector3 curvedVelocity      = GetSpinAdjustedVelocity(baseVelocity, currentSpin);
        float   totalCurveIntensity = CalculateSpinCurveIntensity(currentSpin);

        trajectoryProjection.ShowSpinCurvePreview(transform.position, curvedVelocity, totalCurveIntensity, currentSpin);
    }

    private void HideAimingVisuals()
    {
        aimLine.enabled = false;
        arrowIndicator.gameObject.SetActive(false);
        trajectoryProjection?.HideCurvePreview();
    }

    private bool IsUIBlockingAim()
    {
        if (spinButton != null && spinButton.IsOpen) return true;

        var canvasMgr = FindAnyObjectByType<GameCanvasManager>();
        return canvasMgr != null && canvasMgr.pauseMenuPanel != null && canvasMgr.pauseMenuPanel.activeSelf;
    }

    #endregion

    #region Aim Line Drawing

    private void DrawAimLine(float length)
    {
        Vector2 currentSpin = spinButton?.GetSpinNormalized() ?? Vector2.zero;

        if (currentSpin.magnitude > 0.1f)
            DrawSpinCurvedAimLine(length, currentSpin);
        else
            DrawStraightAimLine(length);
    }

    private void DrawSpinCurvedAimLine(float length, Vector2 spin)
    {
        if (trajectoryProjection == null) { DrawStraightAimLine(length); return; }

        const int points       = 15;
        float     previewPower = isCharging ? currentPower : maxPower * 0.5f;
        Vector3   simVelocity  = GetSpinAdjustedVelocity(aimingSystem.AimDirection * previewPower, spin);
        Vector3   startPos     = transform.position + Vector3.up * 0.05f;

        Vector3[] curvePoints = trajectoryProjection.GetSpinCurvePoints(startPos, simVelocity, spin, points);

        aimLine.positionCount = points;
        for (int i = 0; i < points; i++)
            aimLine.SetPosition(i, curvePoints[i]);
    }

    private void DrawStraightAimLine(float length)
    {
        Vector3 start = transform.position + Vector3.up * 0.05f;
        aimLine.positionCount = 2;
        aimLine.SetPosition(0, start);
        aimLine.SetPosition(1, start + aimingSystem.AimDirection * length);
    }

    #endregion

    #region Spin Helpers

    private float CalculateSpinCurveIntensity(Vector2 spin)
    {
        const float maxSpinCurve = 10.0f;
        return spin.x * maxSpinCurve;
    }

    private Vector3 GetSpinAdjustedVelocity(Vector3 baseVelocity, Vector2 spin)
    {
        if (spin.magnitude < 0.1f) return baseVelocity;

        Vector3 perp = Vector3.Cross(baseVelocity.normalized, Vector3.forward).normalized;

        if (Mathf.Abs(spin.y) > 0.1f)
            baseVelocity *= 1.0f + (spin.y * 0.2f);

        return baseVelocity + perp * (spin.x * 0.3f);
    }

    private bool HasSignificantSpin()
    {
        return (spinButton?.GetSpinNormalized() ?? Vector2.zero).magnitude > 0.1f;
    }

    #endregion

    #region Public Utilities

    public Vector3 GetCurrentVelocity() => billiardBall.Velocity;

    #endregion

    #region One Hit Kill Upgrade

    public void EnableOneHitKill()
    {
        oneHitKillActive = true;
        if (billiardBall != null)
        {
            billiardBall.oneHitKillActive = true;
        }
    }

    public void DisableOneHitKill()
    {
        oneHitKillActive = false;
        if (billiardBall != null)
        {
            billiardBall.oneHitKillActive = false;
        }
    }

    public bool HasOneHitKill()
    {
        return oneHitKillActive;
    }

    #endregion
}