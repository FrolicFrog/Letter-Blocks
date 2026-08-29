using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections.Generic; // Required for the List

[RequireComponent(typeof(Toggle))]
public class ToggleTransition : MonoBehaviour
{
    // --- Radio & Lock State Variables ---
    private static List<ToggleTransition> allToggles = new List<ToggleTransition>();
    public static bool isInProgress = false;
    private bool isInternalChange = false; // Prevents recursive event loops

    [Tooltip("The UI element you want to shrink (usually the Background).")]
    public Transform targetGraphic;

    [Header("Scale Settings")]
    [Tooltip("The scale when the toggle is ON.")]
    public Vector3 onScale = new Vector3(0.75f, 0.75f, 1f);

    [Tooltip("The scale when the toggle is OFF.")]
    public Vector3 offScale = Vector3.one;

    [Header("Animation Settings")]
    [Tooltip("How long the animation takes.")]
    public float duration = 0.25f;

    [Tooltip("The secret sauce for juiciness. OutBack adds a satisfying bounce/overshoot.")]
    public Ease easeType = Ease.OutBack;

    private Toggle toggle;
    private Tween scaleTween;

    void Awake()
    {
        toggle = GetComponent<Toggle>();
        allToggles.Add(this); // Register this toggle to the group

        toggle.onValueChanged.AddListener(OnToggleValueChanged);

        // Set the initial scale instantly without animation
        targetGraphic.localScale = toggle.isOn ? onScale : offScale;
    }

    void OnToggleValueChanged(bool isOn)
    {
        // Skip if this change was forced by script, not by a user click
        if (isInternalChange) return;

        // 1. Check Lock: If an animation is running and the user tries to turn THIS on, block it
        if (isOn && isInProgress)
        {
            isInternalChange = true;
            toggle.isOn = false; // Revert the toggle visually in the UI
            isInternalChange = false;
            return;
        }

        // 2. Radio Logic: If this turns ON, turn OFF all others and start the lock timer
        if (isOn)
        {
            isInProgress = true; // Lock the group

            foreach (var otherToggle in allToggles)
            {
                if (otherToggle != this && otherToggle.toggle.isOn)
                {
                    otherToggle.ForceOff();
                }
            }

            // Unlock the group after the animation finishes
            DOVirtual.DelayedCall(duration, () => isInProgress = false).SetUpdate(true);
        }

        // 3. Play the animation
        AnimateToggle(isOn);
    }

    // Forces a toggle off via script without triggering the lock logic again
    public void ForceOff()
    {
        isInternalChange = true;
        toggle.isOn = false;
        isInternalChange = false;

        AnimateToggle(false);
    }

    private void AnimateToggle(bool isNowOn)
    {
        scaleTween?.Kill();
        Vector3 targetScale = isNowOn ? onScale : offScale;

        scaleTween = targetGraphic.DOScale(targetScale, duration)
                                  .SetEase(easeType)
                                  .SetUpdate(true);
    }

    void OnDestroy()
    {
        if (allToggles.Contains(this))
        {
            allToggles.Remove(this);
        }

        if (toggle != null)
        {
            toggle.onValueChanged.RemoveListener(OnToggleValueChanged);
        }
        scaleTween?.Kill();
    }
}