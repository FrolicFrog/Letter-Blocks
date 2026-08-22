using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI; // 1. Added namespace for UI components like Image

public class ResultManager : MonoBehaviour
{
    public TextMeshProUGUI tmp;
    public Image timerImage; // 2. Added Image component reference
    public float flashSpeed = 2f; // Speed at which the alpha cycles
    public AudioClip failSound,timerSound;
    public GameObject failMenu, completeMenu;
    public AudioClip complete;
    [HideInInspector] public bool timer;
    [HideInInspector] public float time;
    [HideInInspector] public bool startTimer = false;
    public static ResultManager Instance;
    public static bool levelFailed = false;

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

            if (time <= 0) // Changed to <= 0 for safety
            {
                time = 0;
                failMenu.SetActive(true);
                GetComponent<AudioSource>().Play();
                startTimer = false;
                levelFailed = true;
            }
            UpdateTimerDisplay();

            // 3. Added Alpha Cycling Logic
            if (timerImage != null)
            {
                Color imgColor = timerImage.color;

                if (time == 0)
                {
                    imgColor.a = 0f; // Set to 0 when timer hits 0
                }
                else if (time <= 10f)
                {
                    if(!GetComponent<AudioSource>().isPlaying)
                    {
                        GetComponent<AudioSource>().PlayOneShot(timerSound);
                    }
                    imgColor.a = Mathf.PingPong(Time.time * flashSpeed, 1f);
                }
       
                timerImage.color = imgColor;
            }
        }

        if (LevelManager.Instance.ticks.Count == 0)
        {
            return;
        }
        foreach (var obj in LevelManager.Instance.ticks)
        {
            if (!obj.activeSelf)
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

        if (time <= 10)
        {
            tmp.color = Color.red;
        }
        else
        {
            tmp.color = Color.white;
        }

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
        else
        {
            LevelManager.Instance.ticks.Clear();
            levelFailed = false;
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
        UpdateTimerDisplay();
        if(!timer)
        {
            tmp.text = "";
        }
    }

    IEnumerator ShowScreen(GameObject obj)
    {
        yield return new WaitForSeconds(1.3f);
        Color imgColor = timerImage.color;
        imgColor.a = 0;
        timerImage.color = imgColor;
        GetComponent<AudioSource>().Stop();
        AudioSource.PlayClipAtPoint(complete, Vector3.up);
        completeMenu.SetActive(true);
        startTimer = false;
    }
}