using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CompleteScreen : MonoBehaviour
{
    public List<KeyValueGroup<ImageProgressFiller, MinMax<int>>> features;
    private Dictionary<ImageProgressFiller, MinMax<int>> progress = new();
    public static CompleteScreen instance;
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
        foreach(var key in progress.Keys)
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

                float percent = 0f;
                float range = maxLevel - minLevel;

                if (range > 0f)
                {
                    percent = ((curLevel - minLevel) / range) * 100f;
                }

                // --- NEW CODE ---
                // If it is exactly the minimum level, force the percentage to be 5%
                if (curLevel == minLevel)
                {
                    percent = 10f;
                }
                // ----------------

                int displayPercent = Mathf.RoundToInt(percent);

                key.tmp.text = displayPercent + "%";
                key.currentPercentage = 100 - displayPercent;

                key.gameObject.SetActive(true);
                break;
            }
        }
    }
}