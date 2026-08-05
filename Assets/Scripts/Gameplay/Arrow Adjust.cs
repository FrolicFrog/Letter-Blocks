using UnityEngine;

public class ArrowAdjust : MonoBehaviour
{
    [Header("Bounds Settings")]
    [Tooltip("If true, calculates bounds using the Collider. If false, uses the Renderer.")]
    public bool useColliderBounds = false;

    [Tooltip("Distance to keep away from the extreme left and right edges.")]
    public float padding = 0f;

    [Header("Execution")]
    [Tooltip("If true, arranges the objects automatically when the game starts.")]
    public bool executeOnStart = true;

    void Start()
    {
        if (executeOnStart)
        {
            DistributeObjects();
        }
    }

    // This attribute allows you to run the script directly from the Unity Editor by right-clicking the component!
    [ContextMenu("Distribute Objects Now")]
    public void DistributeObjects()
    {
        Bounds parentBounds = new Bounds();
        bool boundsFound = false;

        // 1. Find the bounds of the parent object
        if (useColliderBounds)
        {
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                parentBounds = col.bounds;
                boundsFound = true;
            }
            else
            {
                Debug.LogWarning("No Collider found on the parent object!");
            }
        }
        else
        {
            Renderer rend = GetComponent<Renderer>();
            if (rend != null)
            {
                parentBounds = rend.bounds;
                boundsFound = true;
            }
            else
            {
                Debug.LogWarning("No Renderer found on the parent object!");
            }
        }

        if (!boundsFound) return;

        // 2. Count the children
        int childCount = transform.childCount;
        if (childCount == 0)
        {
            Debug.Log("No child objects to distribute.");
            return;
        }

        // 3. Calculate start and end points along the X axis
        float startX = parentBounds.min.x + padding;
        float endX = parentBounds.max.x - padding;

        // 4. Distribute each child
        for (int i = 0; i < childCount; i++)
        {
            Transform child = transform.GetChild(i);

            // If there's only one child, center it. Otherwise, spread evenly (0.0 to 1.0).
            float percentage = (childCount == 1) ? 0.5f : (float)i / (childCount - 1);

            // Calculate the exact X world coordinate
            float newX = Mathf.Lerp(startX, endX, percentage);

            // Apply the new position while keeping the child's original Y and Z world positions
            child.position = new Vector3(newX, child.position.y, child.position.z);
        }
    }
}