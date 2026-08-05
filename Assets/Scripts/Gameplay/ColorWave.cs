using System.Collections.Generic;
using UnityEngine;

public class ColorWave : MonoBehaviour
{
    [Header("Color Settings")]
    [Tooltip("The default resting color (e.g., White)")]
    public Color baseColor = Color.white;

    [Tooltip("The color that flows through the objects (e.g., Blue)")]
    public Color highlightColor = Color.blue;

    [Header("Chase Settings")]
    [Tooltip("How fast the wave travels (arrows per second). Higher = faster.")]
    public float speed = 5f;

    [Tooltip("How many arrows the fading blue tail should cover.")]
    public float tailLength = 3f;

    [Tooltip("How many empty 'invisible' arrows of space to leave before starting the next wave loop.")]
    public float waveGap = 1f;

    [Tooltip("Check this if the flow is moving backwards instead of forwards!")]
    public bool reverseDirection = false;

    // List to store our child SpriteRenderers
    private List<SpriteRenderer> childRenderers = new List<SpriteRenderer>();

    private void Start()
    {
        // Collect all SpriteRenderers from the immediate children
        foreach (Transform child in transform)
        {
            SpriteRenderer sr = child.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                childRenderers.Add(sr);
            }
        }

        if (childRenderers.Count == 0)
        {
            Debug.LogWarning("No SpriteRenderers found on the children of " + gameObject.name);
        }
    }

    private void Update()
    {
        if (childRenderers.Count == 0) return;

        // The total length of our loop = total arrows + whatever empty gap we want between pulses
        float totalCycleLength = childRenderers.Count + waveGap;

        // The "head" of the wave moves forward continuously over time
        float currentHeadPosition = (Time.time * speed) % totalCycleLength;

        for (int i = 0; i < childRenderers.Count; i++)
        {
            // Invert the physical index if moving in reverse
            int logicalIndex = reverseDirection ? (childRenderers.Count - 1 - i) : i;

            // Calculate how far this specific arrow is behind the wave head
            // Mathf.Repeat perfectly wraps the calculation so it seamlessly loops
            float distance = Mathf.Repeat(currentHeadPosition - logicalIndex, totalCycleLength);

            float intensity = 0f;

            // Only light up if the arrow falls within the length of our fading tail
            if (distance < tailLength)
            {
                // 1.0 (brightest) at distance 0, fading smoothly to 0.0 at the end of the tail
                intensity = 1f - (distance / tailLength);
            }

            // Apply the final calculated color
            childRenderers[i].color = Color.Lerp(baseColor, highlightColor, intensity);
        }
    }
}