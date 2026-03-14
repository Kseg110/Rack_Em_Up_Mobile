using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Manages trajectory prediction for the billiard ball.
/// Creates an isolated physics simulation scene to ghost-simulate straight and curved
/// trajectories, and provides a step-simulated spin curve preview line that mirrors
/// the real Magnus force, damping, and spin-decay applied by BilliardBall.
/// </summary>
public class Projection : PhysicsMaterialManager
{
    #region Inspector Fields

    [SerializeField] private Transform _obstaclesParent;
    [SerializeField] private LineRenderer _line;
    [SerializeField] private int _maxPhysicsFrameIterations;
    [SerializeField] private LineRenderer _curvePreviewLine;

    [Header("Curve Preview Physics Match")]
    [Tooltip("Assign the player BilliardBall so the preview uses its exact physics values.")]
    [SerializeField] private BilliardBall _billiardBall;
    [Tooltip("Higher = smoother curve and longer preview. Must exceed positionCount.")]
    [SerializeField] private int _curvePreviewSteps = 200;

    #endregion

    #region Private State

    private Scene        _simulationScene;
    private PhysicsScene _physicsScene;

    #endregion

    #region Unity Lifecycle

    protected override void Start()
    {
        base.Start();
        CreatePhysicsScene();
        SetupCurvePreviewLine();
        ResolveBilliardBallReference();
        EnsureTrajectoryLine();
    }

    #endregion

    #region Scene Setup

    private void CreatePhysicsScene()
    {
        _simulationScene = SceneManager.CreateScene("Simulation", new CreateSceneParameters(LocalPhysicsMode.Physics3D));
        _physicsScene    = _simulationScene.GetPhysicsScene();

        if (_obstaclesParent == null)
        {
            Debug.LogWarning("[Projection] _obstaclesParent not assigned — no obstacle ghosts created.");
            return;
        }

        foreach (Transform obj in _obstaclesParent)
        {
            var ghost = Instantiate(obj.gameObject, obj.position, obj.rotation);

            var ghostRenderer = ghost.GetComponent<Renderer>();
            if (ghostRenderer != null) ghostRenderer.enabled = false;

            var wallCollider = ghost.GetComponent<Collider>();
            if (wallCollider != null) wallCollider.material = GetWallMaterial();

            SceneManager.MoveGameObjectToScene(ghost, _simulationScene);
        }
    }

    private void SetupCurvePreviewLine()
    {
        if (_curvePreviewLine == null)
        {
            var obj = new GameObject("CurvePreviewLine");
            _curvePreviewLine = obj.AddComponent<LineRenderer>();
        }

        _curvePreviewLine.positionCount = 80;
        _curvePreviewLine.startWidth    = 0.03f;
        _curvePreviewLine.endWidth      = 0.03f;
        _curvePreviewLine.material      = new Material(Shader.Find("Sprites/Default"));
        _curvePreviewLine.startColor    = Color.yellow;
        _curvePreviewLine.endColor      = Color.white;
        _curvePreviewLine.enabled       = false;
    }

    private void EnsureTrajectoryLine()
    {
        if (_line != null) return;

        Debug.LogWarning("[Projection] _line not assigned — creating fallback LineRenderer.");
        var obj = new GameObject("TrajectoryLine");
        _line             = obj.AddComponent<LineRenderer>();
        _line.material    = new Material(Shader.Find("Sprites/Default"));
        _line.startWidth  = 0.03f;
        _line.endWidth    = 0.03f;
        _line.startColor  = Color.white;
        _line.endColor    = Color.white;
        _line.enabled     = false;
    }

    private void ResolveBilliardBallReference()
    {
        if (_billiardBall != null) return;

        _billiardBall = FindFirstObjectByType<BilliardBall>();
        if (_billiardBall == null)
            Debug.LogWarning("[Projection] BilliardBall not found — curve preview uses default physics values.");
        else
            Debug.Log($"[Projection] Auto-found BilliardBall '{_billiardBall.name}'. CurveStrength={_billiardBall.curveStrength}");
    }

    #endregion

    #region Trajectory Simulation (Physics Scene)

    public void SimulateTrajectory(BilliardController playerCueBall, Vector2 pos, Vector2 velocity)
    {
        if (!IsSimulationReady()) return;

        var ghost  = CreateGhost(playerCueBall, pos);
        var ghostRb = ghost.GetComponent<Rigidbody>();
        ghost.Shoot(velocity);

        RunSimulation(ghostRb, _maxPhysicsFrameIterations);
        DestroyImmediate(ghost.gameObject);
    }

    public void SimulateCurvedTrajectory(BilliardController playerCueBall, Vector2 pos, Vector3 curvedVelocity, float curveIntensity)
    {
        if (!IsSimulationReady()) return;

        var ghost           = CreateGhost(playerCueBall, pos);
        var ghostRb         = ghost.GetComponent<Rigidbody>();
        var ghostBilliardBall = ghost.GetComponent<BilliardBall>();

        if (ghostBilliardBall != null)
            ghostBilliardBall.ApplyForceWithCurve(curvedVelocity, Vector3.Cross(curvedVelocity.normalized, Vector3.forward), curveIntensity);
        else if (ghostRb != null)
            ghostRb.AddForce(curvedVelocity, ForceMode.Impulse);

        RunSimulation(ghostRb, _maxPhysicsFrameIterations);
        DestroyImmediate(ghost.gameObject);
    }

    private BilliardController CreateGhost(BilliardController source, Vector2 pos)
    {
        Vector3 position3D = new Vector3(pos.x, source.transform.position.y, pos.y);
        var ghost = Instantiate(source, position3D, Quaternion.identity);

        var ghostRenderer = ghost.GetComponent<Renderer>();
        if (ghostRenderer != null) ghostRenderer.enabled = false;

        var ballCollider = ghost.GetComponent<Collider>();
        if (ballCollider != null) ballCollider.material = GetBallMaterial();

        var ghostRb = ghost.GetComponent<Rigidbody>();
        if (ghostRb != null) RigidbodyConfigurator.ConfigureBilliardBall(ghostRb);

        SceneManager.MoveGameObjectToScene(ghost.gameObject, _simulationScene);
        return ghost;
    }

    private void RunSimulation(Rigidbody ghostRb, int iterations)
    {
        if (_line == null) return;

        _line.positionCount = iterations;

        for (int i = 0; i < iterations; i++)
        {
            _physicsScene.Simulate(Time.fixedDeltaTime);
            _line.SetPosition(i, ghostRb.position);

            if (ghostRb.linearVelocity.magnitude < 0.1f)
            {
                for (int j = i + 1; j < iterations; j++)
                    _line.SetPosition(j, ghostRb.position);
                break;
            }
        }
    }

    private bool IsSimulationReady()
    {
        if (_simulationScene.IsValid() && _physicsScene.IsValid()) return true;
        Debug.LogWarning("[Projection] Simulation scene not available.");
        return false;
    }

    #endregion

    #region Spin Curve Preview

    /// <summary>
    /// Returns world-space points by step-simulating the same Magnus force, damping,
    /// and spin-decay math as BilliardBall.FixedUpdate. Pass the raw force vector —
    /// it is divided by mass internally, matching BilliardBall.ApplyForce.
    /// </summary>
    public Vector3[] GetSpinCurvePoints(Vector3 startPos, Vector3 velocity, Vector2 spin, int pointCount)
    {
        if (_billiardBall == null) ResolveBilliardBallReference();

        float curveStrength = _billiardBall != null ? _billiardBall.curveStrength        : 15f;
        float topSpinEffect = _billiardBall != null ? _billiardBall.topSpinEffect         : 8f;
        float spinDecayRate = _billiardBall != null ? _billiardBall.spinDecayRate         : 0.5f;
        float stopThreshold = _billiardBall != null ? _billiardBall.stopVelocityThreshold : 0.1f;
        float mass          = _billiardBall != null ? _billiardBall.GetComponent<Rigidbody>().mass : 1f;

        const float linearDamping = 0.95f;
        const float dt            = 1f / 50f;

        Vector3 simVelocity   = velocity / mass;
        Vector2 simSpin       = spin;
        Vector3 simPos        = startPos;
        float   dampingFactor = Mathf.Pow(linearDamping, dt * 60f);

        int   totalSteps     = Mathf.Max(_curvePreviewSteps, pointCount * 2);
        float sampleInterval = totalSteps / (float)(pointCount - 1);

        Vector3[] points = new Vector3[pointCount];
        points[0] = simPos;
        int pointIndex = 0;

        for (int step = 1; step <= totalSteps && pointIndex < pointCount - 1; step++)
        {
            if (Mathf.Abs(simSpin.x) > 0.01f && simVelocity.magnitude > 0.01f)
            {
                Vector3 perp      = Vector3.Cross(simVelocity.normalized, Vector3.forward).normalized;
                float   velMul    = Mathf.Clamp(simVelocity.magnitude, 1f, 12f);
                float   magnusAcc = -simSpin.x * curveStrength * velMul;
                simVelocity      += perp * magnusAcc * dt;
            }

            if (Mathf.Abs(simSpin.y) > 0.01f && simVelocity.magnitude > 0.01f)
                simVelocity += simVelocity.normalized * (simSpin.y * topSpinEffect * dt);

            simVelocity *= dampingFactor;

            if (simVelocity.magnitude < stopThreshold)
            {
                for (int r = pointIndex + 1; r < pointCount; r++)
                    points[r] = simPos;
                return points;
            }

            simSpin = Vector2.MoveTowards(simSpin, Vector2.zero, spinDecayRate * dt);
            simPos += simVelocity * dt;

            if (step >= pointIndex * sampleInterval)
            {
                pointIndex++;
                if (pointIndex < pointCount)
                    points[pointIndex] = simPos;
            }
        }

        for (int r = pointIndex; r < pointCount; r++)
            points[r] = simPos;

        return points;
    }

    public void ShowSpinCurvePreview(Vector3 startPos, Vector3 velocity, float curveIntensity, Vector2 spin)
    {
        if (spin.magnitude < 0.5f && Mathf.Abs(curveIntensity) < 0.1f)
        {
            _curvePreviewLine.enabled = false;
            return;
        }

        _curvePreviewLine.enabled = true;

        int       pointCount = _curvePreviewLine.positionCount;
        Vector3[] points     = GetSpinCurvePoints(startPos, velocity, spin, pointCount);

        for (int i = 0; i < pointCount; i++)
            _curvePreviewLine.SetPosition(i, points[i]);

        UpdateCurveLineColor(spin);
    }

    public void ShowCurvePreview(Vector3 startPos, Vector3 velocity, float curveIntensity)
    {
        ShowSpinCurvePreview(startPos, velocity, curveIntensity, Vector2.zero);
    }

    public void HideCurvePreview()
    {
        if (_curvePreviewLine != null)
            _curvePreviewLine.enabled = false;
    }

    private void UpdateCurveLineColor(Vector2 spin)
    {
        Color startColor, endColor;

        if (Mathf.Abs(spin.x) > Mathf.Abs(spin.y))
        {
            startColor = spin.x > 0f ? Color.cyan : Color.magenta;
        }
        else
        {
            startColor = spin.y > 0f ? Color.green : Color.red;
        }

        endColor = Color.Lerp(startColor, Color.white, 0.3f);

        _curvePreviewLine.startColor = startColor;
        _curvePreviewLine.endColor   = endColor;
    }

    #endregion
}
