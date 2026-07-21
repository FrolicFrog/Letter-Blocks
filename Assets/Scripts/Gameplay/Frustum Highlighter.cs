using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(Camera))]
[ExecuteInEditMode]
public class CameraFloorHighlighter : MonoBehaviour
{
    [Header("Highlight Settings")]
    public Color highlightColor = new Color(0f, 1f, 0f, 0.15f);
    public Color outlineColor = new Color(0f, 1f, 0f, 0.8f);
    public bool showHighlight = true;

    [Header("Floor Settings")]
    [Tooltip("The Y-axis level where the theoretical floor exists.")]
    public float floorHeight = 0f;

    [Header("Center Tracking")]
    [Tooltip("The primary object to keep centered in the frustum's floor area.")]
    public Transform centerTarget;
    [Tooltip("Position offset applied to the primary center target.")]
    public Vector3 centerOffset = Vector3.zero;

    [Tooltip("The secondary object to keep centered in the frustum's floor area.")]
    public Transform secondaryCenterTarget;
    [Tooltip("Position offset applied to the secondary center target.")]
    public Vector3 secondaryCenterOffset = Vector3.zero;

    private void Start()
    {
        Application.targetFrameRate = 60;
    }
    private void Update()
    {
        // Only run the calculations if we actually have at least one target to move
        if (centerTarget == null && secondaryCenterTarget == null) return;

        Vector3[] points = GetFrustumFloorPoints();
        if (points == null || points.Length != 4) return;

        // Calculate the exact center by averaging the 4 corners of the frustum on the floor
        Vector3 centerPoint = (points[0] + points[1] + points[2] + points[3]) / 4f;

        // Apply the offset and move the primary target
        if (centerTarget != null)
        {
            centerTarget.position = centerPoint + centerOffset;
        }

        // Apply the offset and move the secondary target
        if (secondaryCenterTarget != null)
        {
            secondaryCenterTarget.position = centerPoint + secondaryCenterOffset;
        }
    }

    /// <summary>
    /// Calculates where the camera's 4 viewport corners intersect with the floor plane.
    /// </summary>
    private Vector3[] GetFrustumFloorPoints()
    {
        Camera cam = GetComponent<Camera>();
        if (cam == null) return null;

        Plane floorPlane = new Plane(Vector3.up, new Vector3(0, floorHeight, 0));

        Vector2[] viewportCorners = new Vector2[]
        {
            new Vector2(0, 0), // Bottom Left
            new Vector2(0, 1), // Top Left   
            new Vector2(1, 1), // Top Right  
            new Vector2(1, 0)  // Bottom Right
        };

        Vector3[] floorPoints = new Vector3[4];

        for (int i = 0; i < 4; i++)
        {
            Ray ray = cam.ViewportPointToRay(viewportCorners[i]);

            if (floorPlane.Raycast(ray, out float distance))
            {
                if (distance > cam.farClipPlane)
                {
                    // Hit is beyond the camera's viewing distance, clamp to farClipPlane
                    Vector3 farPoint = ray.GetPoint(cam.farClipPlane);
                    floorPoints[i] = new Vector3(farPoint.x, floorHeight, farPoint.z);
                }
                else
                {
                    // Valid intersection within range
                    floorPoints[i] = ray.GetPoint(distance);
                }
            }
            else
            {
                // Looking away from the floor, project far clip plane down to floor height
                Vector3 farPoint = ray.GetPoint(cam.farClipPlane);
                floorPoints[i] = new Vector3(farPoint.x, floorHeight, farPoint.z);
            }
        }

        return floorPoints;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (showHighlight)
        {
            Vector3[] points = GetFrustumFloorPoints();
            if (points != null && points.Length == 4)
            {
                // Draws the semi-transparent polygon in the Scene View
                Handles.DrawSolidRectangleWithOutline(points, highlightColor, outlineColor);
            }
        }
    }
#endif
}