using UnityEngine;

/// <summary>
/// Handles touch-based aiming for the billiard cue ball.
/// Converts screen-space touch/drag input into a world-space aim direction by
/// raycasting against the game plane, and maintains the current aim line length
/// via a ground-layer raycast.
/// </summary>
[System.Serializable]
public class AimingSystem
{
    #region Inspector Fields

    [Header("Aiming Settings")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float lineLength = 4.0f;

    [Header("Touch Settings")]
    [SerializeField] private float touchSensitivity  = 1.0f;
    [SerializeField] private float minTouchDistance  = 1f;
    [SerializeField] private bool  enableTouchAiming = true;

    [Header("Curve Shot Settings")]
    [SerializeField] private float maxCurveIntensity = 3.0f;

    #endregion

    #region Public Properties

    public Vector3 AimDirection        { get; private set; } = Vector3.right;
    public float   CurrentAimLineLength { get; private set; } = 1.0f;
    public float   CurveIntensity      { get; private set; }
    public bool    IsCurveShotActive   { get; private set; }

    #endregion

    #region Private State

    private Camera    mainCam;
    private Transform ballTransform;

    private Vector2 dragStartScreenPos;
    private Vector2 lastTouchScreenPos;
    private bool    isDragging;

    #endregion

    #region Initialization

    public void Initialize(Camera camera, Transform ball)
    {
        mainCam        = camera;
        ballTransform  = ball;
        AimDirection   = Vector3.right;
    }

    #endregion

    #region Update

    public void UpdateAiming()
    {
        bool touching = InputManager.Instance != null && InputManager.Instance.IsTouching();

        if (touching)
        {
            Vector2 screenPos = InputManager.Instance.GetTouchScreenPosition();
            if (screenPos == Vector2.zero) return;

            if (!isDragging)
            {
                isDragging         = true;
                dragStartScreenPos = screenPos;
            }

            lastTouchScreenPos = screenPos;

            Vector2 aimPos = enableTouchAiming
                ? dragStartScreenPos + (screenPos - dragStartScreenPos) * touchSensitivity
                : screenPos;

            UpdateAimingFromScreenPosition(aimPos);
        }
        else if (isDragging)
        {
            isDragging = false;

            if (lastTouchScreenPos != Vector2.zero)
            {
                Vector2 aimPos = enableTouchAiming
                    ? dragStartScreenPos + (lastTouchScreenPos - dragStartScreenPos) * touchSensitivity
                    : lastTouchScreenPos;

                UpdateAimingFromScreenPosition(aimPos);
                lastTouchScreenPos = Vector2.zero;
            }
        }

        UpdateAimLineLength();
    }

    public void OnTouchRelease()
    {
        if (InputManager.Instance == null) return;
        Vector2 releasePos = InputManager.Instance.GetTouchScreenPosition();
        if (releasePos != Vector2.zero)
            UpdateAimingFromScreenPosition(releasePos);
    }

    // Kept for event subscription compatibility in BilliardController
    public void OnTouchEnd() { }

    #endregion

    #region Private Helpers

    private void UpdateAimingFromScreenPosition(Vector2 screenPos)
    {
        if (screenPos == Vector2.zero || mainCam == null || ballTransform == null) return;

        Ray   ray       = mainCam.ScreenPointToRay(screenPos);
        Plane gamePlane = new Plane(Vector3.forward, new Vector3(0f, 0f, ballTransform.position.z));

        if (gamePlane.Raycast(ray, out float distance))
        {
            Vector3 worldPos  = ray.GetPoint(distance);
            Vector3 direction = worldPos - ballTransform.position;
            direction.z       = 0f;

            if (direction.magnitude >= minTouchDistance && direction.sqrMagnitude > 0.01f)
                AimDirection = direction.normalized;
        }
    }

    private void UpdateAimLineLength()
    {
        if (AimDirection == Vector3.zero)
        {
            AimDirection        = Vector3.right;
            CurrentAimLineLength = lineLength;
            return;
        }

        CurrentAimLineLength = Physics.Raycast(ballTransform.position, AimDirection, out RaycastHit hit, lineLength, groundLayer)
            ? hit.distance
            : lineLength;
    }

    #endregion
}