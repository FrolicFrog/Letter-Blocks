using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TutorialPopup : MonoBehaviour
{
    public List<KeyValueGroup<int,GameObject>> group;
    public GameObject image, imagePanel;
    private Dictionary<int, GameObject> levelTutorial =  new();
    public static TutorialPopup instance;
    private Vector3 originalScale;

    public void Setup()
    {
       levelTutorial.Clear();
            originalScale = transform.localScale;
            instance = this;
            foreach (var g in group)
            {
                levelTutorial[g.Key] = g.Value;
            }
        
    }


    public void ShowTutorial()
    {

        if(levelTutorial.ContainsKey( LevelManager.Instance.CurLevelNumber))
        {

            imagePanel.SetActive(true);
          image.transform.DOKill(); // Stop any current tweens to prevent overlapping glitches
          image.transform.localScale = Vector3.zero; // Set to zero
          image.transform.DOScale(originalScale, 0.4f).SetEase(Ease.OutBack);
            levelTutorial[LevelManager.Instance.CurLevelNumber].SetActive(true);
        }
    }
    public void Continue(GameObject Go)
    {
        Go.SetActive(false);
        foreach(var key in levelTutorial.Keys)
        {
            levelTutorial[key].SetActive(false);
        }
    }
}
