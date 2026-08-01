using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ResultManager : MonoBehaviour
{
    public TextMeshProUGUI tmp;
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
    }

    private void UpdateTimerDisplay()
    {
      
        float minutes = Mathf.FloorToInt(time / 60);
        float seconds = Mathf.FloorToInt(time % 60);

       
        tmp.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }
}
