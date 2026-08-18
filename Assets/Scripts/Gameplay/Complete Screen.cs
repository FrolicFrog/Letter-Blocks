using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using TMPro;

public class CompleteScreen : MonoBehaviour
{
    public List<KeyValueGroup<ImageProgressFiller, MinMax<int>>> features;
    public TextMeshProUGUI tmp, tmpMessage;
    private Dictionary<ImageProgressFiller, MinMax<int>> progress = new();
    public static CompleteScreen instance;

    private Vector3 originalScale;

    // Define the duration so both animations sync perfectly
    private float scaleDuration = 0.4f;

    private void Awake()
    {
        originalScale = transform.localScale;
    }

    private void Start()
    {
        instance = this;
        foreach (var feature in features)
        {
            progress[feature.Key] = feature.Value;
        }
    }

    private void OnEnable()
    {
        // --- DOTween Scaling Animation ---
        transform.DOKill();
        transform.localScale = Vector3.zero;
        transform.DOScale(originalScale, scaleDuration).SetEase(Ease.OutBack);
        // ---------------------------------

        tmp.text = "Level " + LevelManager.Instance.CurLevelNumber ;

        if (progress.Count == 0)
        {
            foreach (var feature in features)
            {
                progress[feature.Key] = feature.Value;
            }
        }
        UpdateRevel();
    }

    private void OnDisable()
    {
        foreach (var key in progress.Keys)
        {
            key.gameObject.SetActive(false);
        }
    }

    void UpdateRevel()
    {
        foreach (var key in progress.Keys)
        {
            if (progress[key].IsWithinRange(LevelManager.Instance.CurLevelNumber))
            {
                float curLevel = (float)LevelManager.Instance.CurLevelNumber;
                float minLevel = (float)progress[key].Min;
                float maxLevel = (float)progress[key].Max;

                float targetPercent = 0f;
                float range = maxLevel - minLevel;

                if (range > 0f)
                {
                    targetPercent = ((curLevel - minLevel) / range) * 100f;
                }

                if (curLevel == minLevel)
                {
                    targetPercent = 10f;
                }

                // Turn on the UI object IMMEDIATELY so it scales up with the panel
                key.gameObject.SetActive(true);

                // Reset visual state instantly before the animation starts
                key.tmp.text = "0%";
                key.currentPercentage = 100;

                // Clear the unlock message while counting up
                if (tmpMessage != null)
                {
                    tmpMessage.text = "";
                }

                DOTween.Kill(key);

                // --- DOTween Lerp Animation ---
                DOVirtual.Float(0f, targetPercent, 1.5f, (currentValue) =>
                {
                    int displayPercent = Mathf.RoundToInt(currentValue);

                    // --- NEW CODE ---
                    // Check if the percentage reached 100
                    if (displayPercent >= 100)
                    {
                        key.tmp.text = "";

                        if (tmpMessage != null)
                        {
                            // Note: Using 'unlockMessgae' exactly as written in your prompt
                            tmpMessage.text = key.unlockMessage;
                        }

                        key.currentPercentage = 0;
                    }
                    else
                    {
                        key.tmp.text = displayPercent + "%";
                        key.currentPercentage = 100 - displayPercent;
                    }
                    // ----------------

                })
                .SetDelay(scaleDuration) // <-- WAITS FOR THE SCALE ANIMATION TO FINISH
                .SetEase(Ease.OutCubic)
                .SetId(key);
                // ------------------------------

                break;
            }
        }
    }
}