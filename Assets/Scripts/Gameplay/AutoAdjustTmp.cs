using UnityEngine;
using TMPro;


[RequireComponent(typeof(TextMeshProUGUI))]
[RequireComponent(typeof(RectTransform))]
public class AutoAdjustTMP : MonoBehaviour
{
    [Header("Font Size Settings")]
    public float minFontSize = 10f;
    public float maxFontSize = 72f;

    [Header("Padding (Inside Parent)")]
    public float margin = 5f;

    private TextMeshProUGUI tmpText;
    private RectTransform rectTransform;

    private void Awake()
    {
        CacheComponents();
        SetupTextToFit();
    }

    private void OnEnable()
    {
        CacheComponents();
        SetupTextToFit();
    }

    private void OnValidate()
    {
        CacheComponents();
        SetupTextToFit();
    }

    private void OnRectTransformDimensionsChange()
    {
        CacheComponents();
        SetupTextToFit();
    }

    private void CacheComponents()
    {
        if (tmpText == null) tmpText = GetComponent<TextMeshProUGUI>();
        if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
    }

    /// <summary>
    /// Configures the TextMeshPro component to automatically scale and wrap 
    /// within the bounds of its parent object.
    /// </summary>
    public void SetupTextToFit()
    {
        if (tmpText == null || rectTransform == null) return;

        // 1. Stretch RectTransform to fill the parent Image
        rectTransform.anchorMin = Vector2.zero; // Bottom-Left
        rectTransform.anchorMax = Vector2.one;  // Top-Right
        rectTransform.offsetMin = Vector2.zero; // Clear left/bottom offsets
        rectTransform.offsetMax = Vector2.zero; // Clear right/top offsets
        rectTransform.pivot = new Vector2(0.5f, 0.5f);

        // 2. Enable Word Wrapping (forces words to the next line if there's space)
        tmpText.enableWordWrapping = true;

        // 3. Enable Auto-Sizing (shrinks text if it overflows)
        tmpText.enableAutoSizing = true;
        tmpText.fontSizeMin = minFontSize;
        tmpText.fontSizeMax = maxFontSize;

        // 4. Center the text
        tmpText.alignment = TextAlignmentOptions.Center;

        // 5. Apply margins so text doesn't touch the very edge of the image
        tmpText.margin = new Vector4(margin, margin, margin, margin);
    }
}