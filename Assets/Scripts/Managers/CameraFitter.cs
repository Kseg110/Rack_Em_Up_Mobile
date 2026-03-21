using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraFitter : MonoBehaviour
{
    [Tooltip("The total width of your playable area in Unity world units.")]
    public float targetBoardWidth = 18f;

    [Tooltip("The total height of your playable area in Unity world units.")]
    public float targetBoardHeight = 10f;

    private Camera cam;

    void Start()
    {
        cam = GetComponent<Camera>();
        AdjustCamera();
    }

    void AdjustCamera()
    {
        // Calculate the aspect ratio of the physical device screen
        float screenAspect = (float)Screen.width / (float)Screen.height;

        // Calculate the aspect ratio of your ideal game board
        float targetAspect = targetBoardWidth / targetBoardHeight;

        if (screenAspect >= targetAspect)
        {
            // The screen is wider than your table. Height is the limiting factor.
            // Orthographic size is exactly half the total height.
            cam.orthographicSize = targetBoardHeight / 2f;
        }
        else
        {
            // The screen is narrower than your table. Width is the limiting factor.
            // We must zoom the camera out to fit the sides on the screen.
            float differenceInSize = targetAspect / screenAspect;
            cam.orthographicSize = (targetBoardHeight / 2f) * differenceInSize;
        }
    }
}