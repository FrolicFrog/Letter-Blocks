using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ImageProgressFiller : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Drag the UI Image you want to fill here.")]
    public Image fillImage;
    public TextMeshProUGUI tmp;

    [TextArea]
    public string unlockMessage;
    [Header("Settings")]
    [Range(0f, 100f)]
    public float currentPercentage = 20f; // Set this in the Inspector or via other scripts

    void Update()
    {
        // Make sure the image is assigned to prevent errors
        if (fillImage != null)
        {
           // Debug.Log("Working");
            // Clamp the percentage between 0 and 100 just to be safe
            float clampedPercent = Mathf.Clamp(currentPercentage, 0f, 100f);

            // Unity's fillAmount uses a 0 to 1 scale, so we divide by 100
            fillImage.fillAmount = clampedPercent / 100f;
        }
    }
}