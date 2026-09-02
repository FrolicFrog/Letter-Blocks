using UnityEngine;
using DG.Tweening;

public class UIElementScaler : MonoBehaviour
{
    [Header("Transform Settings")]
    [Tooltip("The UI element that will be scaling.")]
    public RectTransform scalingElement;

    [Tooltip("The position to snap to when scaling starts (local space).")]
    public Vector3 startLocalPosition = Vector3.zero;

    [Header("Scale Settings")]
    [Tooltip("The starting scale.")]
    public Vector3 startScale = Vector3.one;

    [Tooltip("The destination scale to animate towards.")]
    public Vector3 destinationScale = new Vector3(1.2f, 1.2f, 1.2f);

    [Tooltip("Time it takes to scale one way.")]
    public float scaleDuration = 0.5f;

    private Tween scaleTween;

    /// <summary>
    /// Enables the element, snaps its position and start scale, and pulses it to the destination scale.
    /// </summary>
    public void StartScaling()
    {
        if (scalingElement == null) return;

        // 1. Enable the object, snap it to the starting position and starting scale
        scalingElement.gameObject.SetActive(true);
        scalingElement.localPosition = startLocalPosition;
        scalingElement.localScale = startScale;

        // Kill any existing tween to avoid overlapping animations
        scaleTween?.Kill();

        // 2. Scale towards the specific destination Vector3
        scaleTween = scalingElement.DOScale(destinationScale, scaleDuration)
                                 .SetEase(Ease.InOutSine)
                                 .SetLoops(-1, LoopType.Yoyo)
                                 .SetUpdate(true);
    }

    /// <summary>
    /// Kills the scale animation and disables the element.
    /// </summary>
    public void StopScaling()
    {
        if (scalingElement == null) return;
        Debug.Log("Stopping Scale");
        scaleTween?.Kill();
        scalingElement.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        // Clean up the tween when this object is destroyed
        scaleTween?.Kill();
    }
}