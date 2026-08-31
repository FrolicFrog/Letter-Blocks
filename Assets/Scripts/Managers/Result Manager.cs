using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening; // 1. Added DOTween namespace

public class ResultManager : MonoBehaviour
{
    public TextMeshProUGUI tmp;
    public Image timerImage;
    public float flashSpeed = 2f;
    public AudioClip failSound, timerSound;
    public GameObject failMenu, completeMenu;
    public Toggle freezeTime;
    public Slider freezeSlider;
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
        if (timer && startTimer && !freezeTime.isOn)
        {
            time -= Time.deltaTime;

            if (time <= 0)
            {
                Taptic.Failure();
                time = 0;
                failMenu.SetActive(true);
                GetComponent<AudioSource>().Play();
                startTimer = false;
                levelFailed = true;
            }
            UpdateTimerDisplay();

            if (timerImage != null)
            {
                Color imgColor = timerImage.color;

                if (time == 0)
                {
                    imgColor.a = 0f;
                }
                else if (time <= 10f)
                {
                    if (!GetComponent<AudioSource>().isPlaying)
                    {
                        GetComponent<AudioSource>().PlayOneShot(timerSound);
                    }
                    imgColor.a = Mathf.PingPong(Time.time * flashSpeed, 1f);
                }

                timerImage.color = imgColor;
            }
        }
        else if (freezeTime.isOn)
        {
            freezeSlider.value -= Time.deltaTime;
            freezeSlider.GetComponentInChildren<TextMeshProUGUI>().text = ((int)freezeSlider.value).ToString();

            if (freezeSlider.value <= 0)
            {
                freezeTime.isOn = false; // Stop updating immediately

                // Improved Disabling Animation (Anticipation Squish + Pop Out)
                freezeSlider.transform.DOKill(); // Always kill before starting a new complex tween

                Sequence closeSeq = DOTween.Sequence();
                // 1. Squish it slightly (Anticipation)
                closeSeq.Append(freezeSlider.transform.DOPunchScale(new Vector3(0.2f, -0.2f, 0f), 0.2f, 2, 0.5f));
                // 2. Shrink it away with an exaggerated InBack
                closeSeq.Append(freezeSlider.transform.DOScale(Vector3.zero, 0.3f).SetEase(Ease.InBack, 1.5f))
                        .OnComplete(() =>
                        {
                            freezeSlider.gameObject.SetActive(false);
                            freezeTime.interactable = true;
                        });
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
        Taptic.Vibrate();
    }

    IEnumerator Inittalize()
    {
        yield return new WaitForEndOfFrame();
        LevelManager.Instance.Initialize();

        completeMenu.SetActive(false);
        failMenu.SetActive(false);
        UpdateTimerDisplay();
        if (!timer)
        {
            tmp.text = "";
        }
    }

    IEnumerator ShowScreen(GameObject obj)
    {
        yield return new WaitForSeconds(1.3f);
        Taptic.Success();
        Color imgColor = timerImage.color;
        imgColor.a = 0;
        timerImage.color = imgColor;
        GetComponent<AudioSource>().Stop();
        AudioSource.PlayClipAtPoint(complete, Vector3.up);
        completeMenu.SetActive(true);
        startTimer = false;
    }

    public void InitalizeToggle()
    {
        if (!freezeTime.isOn)
        {
            return;
        }
        freezeTime.interactable = false;
        freezeSlider.gameObject.SetActive(true);
        freezeSlider.value = freezeSlider.maxValue;
        freezeSlider.GetComponentInChildren<TextMeshProUGUI>().text = freezeSlider.maxValue.ToString();
        PowerUpLockManager.Instance.UpdatePowerUpQuantity(9, -1);

        // 3. DOTween Enabling Animation (Juicy Pop In)
        freezeSlider.transform.DOKill(); // Prevent overlapping tweens
        freezeSlider.transform.localScale = Vector3.zero; // Start small
        freezeSlider.transform.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack); // Pop to full size with overshoot
    }
}