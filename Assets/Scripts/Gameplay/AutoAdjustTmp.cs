using UnityEngine;
using TMPro;

[RequireComponent(typeof(TextMeshProUGUI))]
[RequireComponent(typeof(RectTransform))]
public class AutoAdjustTMP : MonoBehaviour
{
    [Header("Font Size Settings")]
    public float minFontSize = 10f;
    public float maxFontSize = 35f;

    [Header("Text Formatting")]
    [Tooltip("If true, automatically replaces spaces with new lines to stack words.")]
    public bool forceStackWords = true;

    [Header("Padding (Inside Parent)")]
    public float margin = 5f;

    private TextMeshProUGUI tmpText;
    private RectTransform rectTransform;

    private void Awake()
    {
        CacheComponents();
        SetupTextToFit();
        FormatText();
    }

    private void OnEnable()
    {
        CacheComponents();
        SetupTextToFit();
        FormatText();
    }

    private void Update()
    {
        // Continuously check and apply formatting every frame
        CacheComponents();
        SetupTextToFit();
        FormatText();
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

    public void SetupTextToFit()
    {
        if (tmpText == null || rectTransform == null) return;

        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);

        tmpText.enableWordWrapping = false;
        tmpText.overflowMode = TextOverflowModes.Truncate;

        tmpText.enableAutoSizing = true;
        tmpText.fontSizeMin = minFontSize;
        tmpText.fontSizeMax = maxFontSize;

        tmpText.alignment = TextAlignmentOptions.Center;
        tmpText.margin = new Vector4(margin, margin, margin, margin);
    }

    /// <summary>
    /// Replaces spaces with newlines (and vice versa) based on the toggle.
    /// </summary>
    private void FormatText()
    {
        if (tmpText == null) return;

        if (forceStackWords)
        {
            // If toggled ON: Replace spaces with line breaks
            if (tmpText.text.Contains(" "))
            {
                tmpText.text = tmpText.text.Replace(" ", "\n");
            }
        }
        else
        {
            // If toggled OFF: Revert line breaks back into spaces
            if (tmpText.text.Contains("\n"))
            {
                tmpText.text = tmpText.text.Replace("\n", " ");
            }
        }
    }
}