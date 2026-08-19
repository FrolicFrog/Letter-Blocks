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

        tmp.text = "Level " + LevelManager.Instance.CurLevelNumber;

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
                float range = maxLevel - minLevel;

                float startPercent = 0f;
                float targetPercent = 0f;

                if (range > 0f)
                {
                    // If this is the absolute minimum level, start from 0 and go to 10
                    if (curLevel == minLevel)
                    {
                        startPercent = 0f;
                        targetPercent = 10f;
                    }
                    else
                    {
                        // Calculate target percent for current level
                        targetPercent = ((curLevel - minLevel) / range) * 100f;

                        // Calculate start percent based on the previous level
                        float prevLevel = curLevel - 1f;

                        // If the previous level was the minLevel, we forced it to 10%
                        if (prevLevel == minLevel)
                        {
                            startPercent = 10f;
                        }
                        else
                        {
                            startPercent = ((prevLevel - minLevel) / range) * 100f;
                        }
                    }
                }

                // Clamp values just to be safe from going over 100
                startPercent = Mathf.Clamp(startPercent, 0f, 100f);
                targetPercent = Mathf.Clamp(targetPercent, 0f, 100f);

                // Turn on the UI object IMMEDIATELY so it scales up with the panel
                key.gameObject.SetActive(true);

                // Initialize visual state to the START percent instantly before animation
                int initialDisplayPercent = Mathf.RoundToInt(startPercent);
                key.tmp.text = initialDisplayPercent + "%";
                key.currentPercentage = 100 - initialDisplayPercent;

                // Clear the unlock message while counting up so it only shows at 100%
                if (tmpMessage != null)
                {
                    tmpMessage.text = "Feature Unlocked!";
                }

                DOTween.Kill(key);

                // --- DOTween Lerp Animation ---
                // Now uses startPercent instead of 0f
                DOVirtual.Float(startPercent, targetPercent, 1.5f, (currentValue) =>
                {
                    int displayPercent = Mathf.RoundToInt(currentValue);

                    // Check if the percentage reached 100
                    if (displayPercent >= 100)
                    {
                        key.tmp.text = "";

                        if (tmpMessage != null)
                        {
                            tmpMessage.text = key.unlockMessage;
                        }

                        key.currentPercentage = 0;
                    }
                    else
                    {
                        key.tmp.text = displayPercent + "%";
                        key.currentPercentage = 100 - displayPercent;
                    }

                })
                .SetDelay(scaleDuration) // WAITS FOR THE SCALE ANIMATION TO FINISH
                .SetEase(Ease.OutCubic)
                .SetId(key);
                // ------------------------------

                break;
            }
        }
    }
}