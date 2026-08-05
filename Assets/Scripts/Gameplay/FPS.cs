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
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 500;
        RenderPipelineAsset currentAsset = GraphicsSettings.currentRenderPipeline;

      
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
