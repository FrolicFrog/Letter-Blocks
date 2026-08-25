using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using TMPro;


public class FPS : MonoBehaviour
{
    float timer;
    int fps;
    TextMeshProUGUI tmp;


    void Start()
    {
        tmp = GetComponent<TextMeshProUGUI>();
       


      
    }


    private void Update()
    {
      
        if(timer >= 1f)
        {
            tmp.text = fps.ToString();
            timer = 0;
            fps = 0;
        }
        timer += Time.deltaTime;
        fps++;
    }
}
