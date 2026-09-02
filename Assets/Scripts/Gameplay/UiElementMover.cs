using UnityEngine;
using DG.Tweening;

public class UIElementMover : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("The UI element that will be moving.")]
    public RectTransform movingElement;

    [Tooltip("The starting position (local space).")]
    public Vector3 startLocalPosition;

    [Tooltip("The destination position (local space) to move towards.")]
    public Vector3 destinationLocalPosition;

    [Tooltip("Time it takes to move one way.")]
    public float moveDuration = 0.5f;

    private Tween moveTween;

    /// <summary>
    /// Enables the moving element, snaps it to the start vector, and moves it to the destination vector.
    /// </summary>
    public void StartMovement()
    {
        if (movingElement == null) return;

        // 1. Enable the object and snap it to the starting position
        movingElement.gameObject.SetActive(true);
        movingElement.localPosition = startLocalPosition;

        // Kill any existing tween to avoid overlapping animations
        moveTween?.Kill();

        // 2. Move towards the specific destination Vector3 in local space
        moveTween = movingElement.DOLocalMove(destinationLocalPosition, moveDuration)
                                 .SetEase(Ease.InOutSine)
                                 .SetLoops(-1, LoopType.Yoyo)
                                 .SetUpdate(true);
    }

    /// <summary>
    /// Kills the movement animation and disables the moving element.
    /// </summary>
    public void StopMovement()
    {
        if (movingElement == null) return;
        Debug.Log("Stoping");
        moveTween?.Kill();
        movingElement.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        // Clean up the tween when this object is destroyed
        moveTween?.Kill();
    }
}