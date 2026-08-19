using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ResultManager : MonoBehaviour
{
    public TextMeshProUGUI tmp;
    public GameObject failMenu, completeMenu;

    [HideInInspector]public bool timer;
    [HideInInspector]public float time;
    [HideInInspector] public bool startTimer = false;
    public static ResultManager Instance;
    void Start()
    {

        Instance = this;
        if (timer)
            UpdateTimerDisplay();

        else
            tmp.text = "";
    }

    void Update()
    {
        if (timer && startTimer)
        {
            time -= Time.deltaTime;
            UpdateTimerDisplay();
            if(time <0)
            {
                startTimer = false;
            }
        }

        if(LevelManager.Instance.ticks.Count ==0)
        {
            return;
        }
        foreach(var obj in LevelManager.Instance.ticks)
        {
            if(!obj.activeSelf)
            {
                return;
            }
        }
        LevelManager.Instance.ticks.Clear();
        StartCoroutine(ShowScreen(completeMenu));
    }

    private void UpdateTimerDisplay()
    {
      
        float minutes = Mathf.FloorToInt(time / 60);
        float seconds = Mathf.FloorToInt(time % 60);

       
        tmp.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }

    public void LoadLevel(bool incrementLevel)
    {
        if (incrementLevel)
        {

            if (!GameManager.Instance.IsTestMode)
            {
                PlayerPrefs.SetInt("LastLevel", PlayerPrefs.GetInt("LastLevel", 1) + 1);
            }
          
        }
        LevelManager.Instance.UnloadInScene();
        StartCoroutine(Inittalize());
       
    }

    IEnumerator Inittalize()
    {
        yield return new WaitForEndOfFrame();
        LevelManager.Instance.Initialize();
        completeMenu.SetActive(false);
        failMenu.SetActive(false);
    }

    IEnumerator ShowScreen(GameObject obj)
    {
        yield return new WaitForSeconds(1.3f);
        completeMenu.SetActive(true);
    }
}
