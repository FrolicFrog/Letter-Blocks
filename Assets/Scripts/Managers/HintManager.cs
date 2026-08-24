using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;

public class HintManager : MonoBehaviour
{
    public Dictionary<string, KeyValueGroup<List<GameObject>, HashSet<int>>> wordChain = new();

    [SerializeField] private LayerMask wordLayer;
    [SerializeField] private float holdThreshold = 0.2f;
    [SerializeField] private FocusCutOut panel;
    [SerializeField] private GameObject tutorialPanel, Hand;

    private float holdTimer = 0f;
    private bool isTracking = false;
    private bool isHoldingTriggered = false;
    private GameObject currentWordObject;
    private Camera mainCamera;
    public static HintManager instance;

    private void Awake()
    {
        mainCamera = Camera.main;
        instance = this;
        if (wordLayer == 0)
        {
            wordLayer = LayerMask.GetMask("Word");
        }
    }

    private void Update()
    {
        // 1. Initial click detection on "Word" layer
        if (Input.GetMouseButtonDown(0) && !ResultManager.levelFailed)
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, wordLayer))
            {
               if(LevelManager.Instance.CurLevelNumber ==2 && tutorialPanel.activeSelf)
                {
                    tutorialPanel.SetActive(false);
                    Hand.SetActive(false);
                }
                isTracking = true;
                currentWordObject = hit.collider.gameObject;
                holdTimer = 0f;
                isHoldingTriggered = false;
                Taptic.Vibrate();
            }
        }

        // 2. Count time while holding down
        if (isTracking && Input.GetMouseButton(0))
        {
            holdTimer += Time.deltaTime;

            if (holdTimer >= holdThreshold && !isHoldingTriggered)
            {
                isHoldingTriggered = true;
                OnHold(currentWordObject);
            }
        }

        // 3. Detect release
        if (Input.GetMouseButtonUp(0))
        {
            if (isHoldingTriggered)
            {
                OnRelease(currentWordObject);
            }

            // Reset state
            isTracking = false;
            isHoldingTriggered = false;
            currentWordObject = null;
            holdTimer = 0f;
        }
    }

    void OnHold(GameObject wordObj)
    {
        panel.gameObject.SetActive(true);

        Canvas canvas = panel.GetComponentInParent<Canvas>();
        RectTransform canvasRect = canvas.transform as RectTransform;

        Vector3 screenPoint = mainCamera.WorldToScreenPoint(wordObj.transform.position);
        Camera uiCamera = (canvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : canvas.worldCamera;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenPoint,
            uiCamera,
            out Vector2 localPoint))
        {
            localPoint.y += 90;
            panel.textBox.anchoredPosition = localPoint;
        }

        // --- Fill text data ---
        FocusCutOut.CutoutGroup group = new();
        foreach (var word in wordChain.Keys.ToList())
        {
            foreach (var obj in wordChain[word].Key.ToList())
            {
                if (obj == null)
                {
                    wordChain[word].Key.Remove(obj);
                    continue;
                }
                if (obj == wordObj)
                {
                    group.renderers = wordChain[word].Key.Select(x => x.GetComponent<Renderer>()).ToList();
                    panel.cutoutGroups.Add(group);

                    TextMeshProUGUI tmpText = panel.textBox.GetComponentInChildren<TextMeshProUGUI>();

                    // 1. Enable Rich Text support & reset base color tint to White
                    tmpText.richText = true;
                    tmpText.color = Color.white;

                    StringBuilder formattedWord = new StringBuilder();
                    HashSet<int> greenIndexes = wordChain[word].Value;

                    // 2. Wrap letters in explicit color tags
                    for (int i = 0; i < word.Length; i++)
                    {
                        if (greenIndexes != null && greenIndexes.Contains(i))
                        {
                            formattedWord.Append($"<color=#00FF00>{word[i]}</color>"); // Green
                        }
                        else
                        {
                            formattedWord.Append($"<color=#333333>{word[i]}</color>"); // Default Dark Gray / Color of choice
                        }
                    }

                    tmpText.text = formattedWord.ToString();
                    return;
                }
            }
        }
    }

    void OnRelease(GameObject wordObj)
    {
        panel.cutoutGroups.Clear();
        panel.gameObject.SetActive(false);
    }
}