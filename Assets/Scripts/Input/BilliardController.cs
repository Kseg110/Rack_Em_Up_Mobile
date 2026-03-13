using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(LineRenderer))]
public class BilliardController : PhysicsMaterialManager
{
    [Header("Power Settings")]
    [SerializeField] private float maxPower = 300f;
    [SerializeField] private float chargeSpeed = 15f;

    [Header("Components")]
    [SerializeField] private BilliardBall billiardBall;
    public AimingSystem aimingSystem; 
    [SerializeField] private RigidbodyConfig rigidbodyConfig;
    [SerializeField] private Projection trajectoryProjection;

    [Header("Visuals")]
    [SerializeField] private Transform arrowIndicator; 
    [SerializeField] private Color minPowerColor = Color.white;
    [SerializeField] private Color maxPowerColor = Color.red;
    [SerializeField] private Color curveColor = Color.yellow;

    [Header("HUD Buttons")]
    [SerializeField] private RadialPowerBar powerBar;
    [SerializeField] private ShootButton shootButton;
    [SerializeField] private SpinButton spinButton; 

    private Rigidbody rb;
    private LineRenderer aimLine;
    private Camera mainCam;

    private bool isCharging;
    private float currentPower;
    private bool ballWasMovingLastFrame = false; // Track ball movement for spin reset

    public float PowerPercentage => currentPower / maxPower;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        aimLine = GetComponent<LineRenderer>();
        mainCam = Camera.main;

        RigidbodyConfigurator.ConfigureRigidbody(rb, rigidbodyConfig);
        
        if (billiardBall == null)
        {
            billiardBall = GetComponent<BilliardBall>();
            if (billiardBall == null)
            {
                billiardBall = gameObject.AddComponent<BilliardBall>();
            }
        }
        
        billiardBall.Initialize(rb, this);
        aimingSystem.Initialize(mainCam, transform);
        
        SetupLineRenderer();
    }

    protected override void Start()
    {
        base.Start();
        ApplyPhysicsMaterial();

        // Find ShootButton 
        if (shootButton == null)
        {
            shootButton = FindFirstObjectByType<ShootButton>();
            if (shootButton == null)
            {
                //Debug.LogWarning("[BilliardController] ShootButton not found in scene!");
            }
        }
        
        // Find SpinButton
        if (spinButton == null)
        {
            spinButton = FindFirstObjectByType<SpinButton>();
            if (spinButton == null)
            {
                Debug.LogWarning("[BilliardController] SpinButton not found in scene!");
            }
        }
        
        SetupShootButton();

        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnTouchEnd += OnTouchEnd;
        }
        
    }

    private void SetupShootButton()
    {
        if (shootButton != null)
        {
            shootButton.OnStartCharging += StartPowerCharging;
            shootButton.OnFireShot += FireShot;
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (shootButton != null)
        {
            shootButton.OnStartCharging -= StartPowerCharging;
            shootButton.OnFireShot -= FireShot;
        }
        
        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnTouchEnd -= OnTouchEnd;
        }
    }

    private void OnTouchEnd()
    {
        aimingSystem.OnTouchRelease();
    }

    void Update()
    {
        bool isBallMoving = billiardBall.IsBallMoving();

        // Check if ball just stopped moving to reset spin
        if (ballWasMovingLastFrame && !isBallMoving)
        {
            // Ball stopped, reset spin 
            if (spinButton != null)
            {
                spinButton.ResetSpin();
                Debug.Log("[BilliardController] Ball stopped, spin reset");
            }
            else
            {
                Debug.LogWarning("[BilliardController] SpinButton reference is null, cannot reset spin");
            }
        }
        ballWasMovingLastFrame = isBallMoving;

        if (isBallMoving)
        {
            aimLine.enabled = false;
            arrowIndicator.gameObject.SetActive(false);
            trajectoryProjection?.HideCurvePreview();
            return;
        }

        // Temporarily disable aiming while UI is open
        var canvasMgr = FindAnyObjectByType<GameCanvasManager>();
        if (canvasMgr != null && canvasMgr.pauseMenuPanel != null && canvasMgr.pauseMenuPanel.activeSelf)
        {
            aimLine.enabled = false;
            arrowIndicator.gameObject.SetActive(false);
            trajectoryProjection?.HideCurvePreview();
            return;
        }

        // Use the assigned spinButton
        if (spinButton != null && spinButton.IsOpen)
        {
            aimLine.enabled = false;
            arrowIndicator.gameObject.SetActive(false);
            trajectoryProjection?.HideCurvePreview();
            return;
        }

        aimingSystem.UpdateAiming();
        
        // Handle charging if in charging state
        if (isCharging)
        {
            HandlePowerCharging();
        }
        
        UpdateVisuals();
        UpdateTrajectoryPreview();
    }

    private void StartPowerCharging()
    {
        if (billiardBall.IsBallMoving())
        {
            return;
        }
        
        isCharging = true;
        currentPower = 0f;
        
        if (powerBar != null) 
            powerBar.SetActive(true);
    }
    
    private void HandlePowerCharging()
    {
        if (!isCharging) return;
        
        currentPower += chargeSpeed * Time.deltaTime;
        currentPower = Mathf.Clamp(currentPower, 0, maxPower);

        if (powerBar != null) 
            powerBar.UpdatePower(PowerPercentage);
            
        // Update button visual feedback
        if (shootButton != null)
        {
            if (PowerPercentage >= 0.8f)
            {
                shootButton.SetReadyToFireState();
            }
            else
            {
                shootButton.SetChargingState();
            }
        }
    }
    
    private void FireShot()
    {
        if (!isCharging)
            return;
        
        Vector3 baseForce = aimingSystem.AimDirection * currentPower;
        
        // Get spin from UI
        Vector2 spin = Vector2.zero;
        if (spinButton != null)
            spin = spinButton.GetSpinNormalized();

        // Enhanced spin feedback - show spin strength in UI
        if (spinButton != null && spin.magnitude > 0.1f)
        {
            Debug.Log($"Applying spin: X={spin.x:F2}, Y={spin.y:F2}");
        }

        // Apply force with spin
        if (HasSignificantSpin())
        {
            billiardBall.ApplyForceWithCurve(baseForce, aimingSystem.CurveIntensity, spin);
        }
        else if (aimingSystem.IsCurveShotActive && Mathf.Abs(aimingSystem.CurveIntensity) > 0.1f)
        {
            billiardBall.ApplyForceWithCurve(baseForce, aimingSystem.CurveIntensity, spin);
        }
        else
        {
            billiardBall.ApplyForce(baseForce, spin);
        }

        // Decrement shots from GameManager
        if (GameManager.Instance != null)
        {
            GameManager.Instance.Shots--;
        }

        // Reset state
        isCharging = false;
        currentPower = 0f;
        
        // Hide UI elements
        if (powerBar != null) 
            powerBar.SetActive(false);
        if (shootButton != null) 
            shootButton.SetIdleState();
            
        aimLine.enabled = false;
        arrowIndicator.gameObject.SetActive(false);
        trajectoryProjection?.HideCurvePreview();
    }

    // Legacy shoot method for compatibility
    public void Shoot()
    {
        FireShot();
    }

    public void Shoot(Vector2 velocity)
    {
        Vector3 force = new Vector3(velocity.x, velocity.y, 0);
        
        // Use explicit spin reset for legacy calls
        billiardBall.ApplyForceAndResetSpin(force);

        // Decrement shots
        if (GameManager.Instance != null)
        {
            GameManager.Instance.Shots--;
        }

        aimLine.enabled = false;
        arrowIndicator.gameObject.SetActive(false);
        trajectoryProjection?.HideCurvePreview();
    }

    private bool HasSignificantSpin()
    {
        if (spinButton == null) return false;
        Vector2 spin = spinButton.GetSpinNormalized();
        return spin.magnitude > 0.1f;
    }

    private void UpdateVisuals()
    {
        bool showAiming = !billiardBall.IsBallMoving();
        
        aimLine.enabled = showAiming;
        arrowIndicator.gameObject.SetActive(showAiming);
        
        if (!showAiming) return;

        DrawAimLine(aimingSystem.CurrentAimLineLength);
        RotateArrow();

        // Enhanced color logic for spin visuals
        Color lineColor;
        Vector2 currentSpin = spinButton?.GetSpinNormalized() ?? Vector2.zero;
        bool hasSignificantSpin = currentSpin.magnitude > 0.1f;
        
        if (isCharging)
        {
            float powerPercent = currentPower / maxPower;
            Color baseColor = aimingSystem.IsCurveShotActive ? curveColor : 
                             hasSignificantSpin ? Color.magenta : minPowerColor; // Magenta for spin
            lineColor = Color.Lerp(baseColor, maxPowerColor, powerPercent);
            
            arrowIndicator.localScale = Vector3.one * (1f + powerPercent * 0.5f);
        }
        else
        {
            lineColor = aimingSystem.IsCurveShotActive ? curveColor : 
                       hasSignificantSpin ? Color.magenta : minPowerColor;
            arrowIndicator.localScale = Vector3.one;
        }
        
        aimLine.startColor = lineColor;
        aimLine.endColor = lineColor;
    }

    private void UpdateTrajectoryPreview()
    {
        if (trajectoryProjection == null) return;

        Vector2 currentSpin = spinButton?.GetSpinNormalized() ?? Vector2.zero;
        bool hasSignificantSpin = currentSpin.magnitude > 0.1f;

        if (hasSignificantSpin || (aimingSystem.IsCurveShotActive && Mathf.Abs(aimingSystem.CurveIntensity) > 0.1f))
        {
            Vector3 baseVelocity = aimingSystem.AimDirection * (currentPower > 0 ? currentPower : 1f);
            
            // Calculate spin-based curve intensity
            float spinCurveIntensity = CalculateSpinCurveIntensity(currentSpin);
            
            // Combine traditional curve with spin curve (spin takes priority)
            float totalCurveIntensity = hasSignificantSpin ? spinCurveIntensity : aimingSystem.CurveIntensity;
            
            Vector3 curvedVelocity = GetSpinAdjustedVelocity(baseVelocity, currentSpin);
            
            trajectoryProjection.ShowSpinCurvePreview(transform.position, curvedVelocity, totalCurveIntensity, currentSpin);
        }
        else
        {
            trajectoryProjection.HideCurvePreview();
        }
    }

    private float CalculateSpinCurveIntensity(Vector2 spin)
    {
        // Convert spin magnitude to curve intensity
        // X spin affects left/right curve
        // Y spin could affect trajectory height/drop
        float spinMagnitude = spin.magnitude;
        float maxSpinCurve = 4.0f; // Maximum curve effect from spin
        
        return spin.x * maxSpinCurve; // Positive X = right curve, Negative X = left curve
    }

    private Vector3 GetSpinAdjustedVelocity(Vector3 baseVelocity, Vector2 spin)
    {
        if (spin.magnitude < 0.1f) return baseVelocity;

        // Apply spin effect to velocity
        // X spin creates lateral curve
        Vector3 perpendicular = Vector3.Cross(baseVelocity.normalized, Vector3.forward).normalized;
        float curveForce = spin.x * 0.3f; // Adjust multiplier for desired curve strength
        
        Vector3 spinEffect = perpendicular * curveForce;
        
        // Y spin could affect the velocity magnitude or add vertical component
        if (Mathf.Abs(spin.y) > 0.1f)
        {
            // Modify velocity magnitude based on Y spin positioning in UI
            float spinModifier = 1.0f + (spin.y * 0.2f);
            baseVelocity *= spinModifier;
        }
        
        return baseVelocity + spinEffect;
    }

    private void RotateArrow()
    {
        if (aimingSystem.AimDirection != Vector3.zero)
        {
            Quaternion lookRot = Quaternion.LookRotation(aimingSystem.AimDirection);
            arrowIndicator.rotation = lookRot;
        }
    }

    private void DrawAimLine(float length)
    {
        Vector2 currentSpin = spinButton?.GetSpinNormalized() ?? Vector2.zero;
        bool hasSignificantSpin = currentSpin.magnitude > 0.1f;

        if (hasSignificantSpin)
        {
            DrawSpinCurvedAimLine(length, currentSpin);
        }
        else if (aimingSystem.IsCurveShotActive && Mathf.Abs(aimingSystem.CurveIntensity) > 0.1f)
        {
            DrawCurvedAimLine(length);
        }
        else
        {
            DrawStraightAimLine(length);
        }
    }

    private void DrawSpinCurvedAimLine(float length, Vector2 spin)
    {
        int points = 15;
        Vector3[] curvePoints = new Vector3[points];
        
        Vector3 startPos = transform.position + Vector3.up * 0.05f;
        Vector3 direction = aimingSystem.AimDirection;
        
        for (int i = 0; i < points; i++)
        {
            float t = i / (float)(points - 1);
            float distance = length * t;
            
            // Base position along aim direction
            Vector3 basePoint = startPos + direction * distance;
            
            // Apply spin curve effect
            Vector3 perpendicular = Vector3.Cross(direction, Vector3.forward).normalized;
            
            // Create a smooth curve based on spin
            float curveAmount = Mathf.Sin(t * Mathf.PI) * spin.x * (length * 0.15f);
            Vector3 curveOffset = perpendicular * curveAmount;
            
            // Apply Y spin as trajectory modification (height variation)
            float heightOffset = spin.y * Mathf.Sin(t * Mathf.PI) * 0.1f;
            Vector3 spinPoint = basePoint + curveOffset + Vector3.up * heightOffset;
            
            curvePoints[i] = spinPoint;
        }
        
        aimLine.positionCount = points;
        for (int i = 0; i < points; i++)
        {
            aimLine.SetPosition(i, curvePoints[i]);
        }
    }

    private void DrawStraightAimLine(float length)
    {
        Vector3 start = transform.position + Vector3.up * 0.05f;
        Vector3 end = start + (aimingSystem.AimDirection * length);
        
        aimLine.positionCount = 2;
        aimLine.SetPosition(0, start);
        aimLine.SetPosition(1, end);
    }

    private void DrawCurvedAimLine(float length)
    {
        Vector3[] curvePoints = aimingSystem.GetCurvePreviewPoints(12);
        
        if (curvePoints != null && curvePoints.Length > 1)
        {
            aimLine.positionCount = curvePoints.Length;
            
            Vector3 startPos = transform.position + Vector3.up * 0.05f;
            float maxDistance = 0f;
            
            for (int i = 1; i < curvePoints.Length; i++)
            {
                float distance = Vector3.Distance(startPos, curvePoints[i]);
                if (distance > maxDistance) maxDistance = distance;
            }
            
            for (int i = 0; i < curvePoints.Length; i++)
            {
                Vector3 scaledPoint;
                if (i == 0)
                {
                    scaledPoint = startPos;
                }
                else
                {
                    Vector3 direction = curvePoints[i] - curvePoints[0];
                    float scale = length / maxDistance;
                    scaledPoint = startPos + direction * scale;
                }
                
                aimLine.SetPosition(i, scaledPoint);
            }
        }
        else
        {
            DrawStraightAimLine(length);
        }
    }

    private void SetupLineRenderer()
    {
        aimLine.positionCount = 2;
        aimLine.enabled = true;
        aimLine.startWidth = 0.05f;
        aimLine.endWidth = 0.05f;   
        aimLine.material = new Material(Shader.Find("Sprites/Default"));
        aimLine.startColor = Color.red;
        aimLine.endColor = Color.red;
    }

    private void ApplyPhysicsMaterial()
    {
        Collider ballCollider = GetComponent<Collider>();
        if (ballCollider != null)
        {
            ballCollider.material = GetBallMaterial();
        }
    }

    // Add this method to get current velocity for predictions
    public Vector3 GetCurrentVelocity()
    {
        return billiardBall.Velocity;
    }
}