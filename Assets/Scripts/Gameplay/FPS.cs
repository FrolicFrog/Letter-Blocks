using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class FPS : MonoBehaviour
{
    float timer;
    int fps;
    TextMeshProUGUI tmp;

    private void Start()
    {
        tmp = GetComponent<TextMeshProUGUI>();
    }
    private void Update()
    {
      
        if(timer >= 1)
        {
            tmp.text = fps.ToString();
            timer = 0;
            fps = 0;
        }
        timer += Time.deltaTime;
        fps++;
    }
}
