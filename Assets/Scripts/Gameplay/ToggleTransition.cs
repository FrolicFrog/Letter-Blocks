using UnityEngine;
using UnityEngine.UI;
using DG.Tweening; // Required for DOTween

[RequireComponent(typeof(Toggle))]
public class ToggleTransition : MonoBehaviour
{
    [Tooltip("The UI element you want to shrink (usually the Background).")]
    public Transform targetGraphic;

    [Header("Scale Settings")]
    [Tooltip("The scale when the toggle is ON.")]
    public Vector3 onScale = new Vector3(0.75f, 0.75f, 1f); // Slightly smaller for a better pop

    [Tooltip("The scale when the toggle is OFF.")]
    public Vector3 offScale = Vector3.one;

    [Header("Animation Settings")]
    [Tooltip("How long the animation takes.")]
    public float duration = 0.25f;

    [Tooltip("The secret sauce for juiciness. OutBack adds a satisfying bounce/overshoot.")]
    public Ease easeType = Ease.OutBack;

    private Toggle toggle;
    private Tween scaleTween; // Stores the active animation

    void Awake()
    {
        toggle = GetComponent<Toggle>();
        toggle.onValueChanged.AddListener(OnToggleValueChanged);

        // Set the initial scale instantly without animation
        targetGraphic.localScale = toggle.isOn ? onScale : offScale;
    }

    void OnToggleValueChanged(bool isOn)
    {
        // 1. Kill any currently running animation so rapid clicks don't break it
        scaleTween?.Kill();

        // 2. Determine target scale
        Vector3 targetScale = isOn ? onScale : offScale;

        // 3. DO the Tween! 
        // SetUpdate(true) ensures it still animates even if Time.timeScale is 0 (game paused)
        scaleTween = targetGraphic.DOScale(targetScale, duration)
                                  .SetEase(easeType)
                                  .SetUpdate(true);
    }

    void OnDestroy()
    {
        // Clean up listeners and kill the tween if the object is destroyed mid-animation
        if (toggle != null)
        {
            toggle.onValueChanged.RemoveListener(OnToggleValueChanged);
        }
        scaleTween?.Kill();
    }
}