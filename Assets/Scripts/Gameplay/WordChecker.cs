using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DG.Tweening;
using TMPro;

public class WordChecker : MonoBehaviour
{
    public static WordChecker instance;

    // Tracks if the whole loop is running (used to prevent double-starts)
    public bool isProcessing = false;

    // NEW FIX: Specifically tracks ONLY the gravity/shifting phase for the Tray Dragger
    public bool isShifting = false;

    private bool dictionarySeparated = false;

    // Tracks slots that have pieces incoming so multiple don't go to the same spot
    public static HashSet<Vector2Int> reservedGridSlots = new HashSet<Vector2Int>();

    // Tracks currently playing destruction animations to safely delay gravity
    private int activeDestructions = 0;

    [Header("Tray to Grid Animation")]
    public float trayJumpDuration = 0.45f;
    public float trayJumpPower = 3.5f;
    public float trayFlightScaleMultiplier = 1.3f;
    public Ease trayJumpEase = Ease.OutQuad;

    [Header("Key to Lock Animation")]
    [Tooltip("Speed in units per second for the key flying to the lock.")]
    public float keySpeed = 15f;
    [Tooltip("Local position offset for the key once it lands on the lock.")]
    public Vector3 keyOffset = Vector3.zero;
    [Tooltip("How high the key arcs during its flight to the lock.")]
    public float keyJumpPower = 2.0f;
    [Tooltip("The final scale of the key when it reaches the lock.")]
    public float keyTargetScale = 0.6f;
    [Tooltip("How long the key waits before detaching and flying to the lock.")]
    public float keyFlightDelay = 0.45f;
    [Tooltip("How long it takes to turn the key inside the lock.")]
    public float keyTurnDuration = 0.3f;
    [Tooltip("How long it takes the lock to shrink and disappear.")]
    public float lockDestroyDuration = 0.3f;

    [Header("Destruction Animation")]
    public float destructionDelay = 0.15f;
    public float popDuration = 0.35f;

    [Header("UI Flight Animation")]
    public Transform categoryUIParent;
    public float flyToUIDuration = 0.5f;
    public float flightElevationOffset = 5.0f;
    public float flightStaggerDelay = 0.06f;
    public Ease flyEase = Ease.InOutQuad;
    public Ease destroyEase = Ease.InQuad;
    public Ease arcEase = Ease.OutCubic;

    [Header("Flight Rotation")]
    public Vector3 flightRotation = new Vector3(0, 0, 360);

    [Header("UI Pop Feedback")]
    public Vector3 uiPopScale = new Vector3(1.15f, 1.15f, 1f);
    public float uiPopDuration = 0.15f;

    [Header("Inverted Arc Settings (Circular Flat Smile)")]
    public float arcRadius = 5.0f;
    public float arcSpacingX = 1.0f;
    public float arcScaleUp = 1.25f;
    public float arcHeightOffset = 1.5f;

    [Header("Juicy Word Gravity Animation")]
    public float gravityShrinkScale = 0.85f;
    public float gravityShrinkDuration = 0.1f;
    public float gravityJumpDuration = 0.25f;
    public float gravityGrowDuration = 0.15f;
    public float gravityJumpPower = 0.2f;

    [Header("Spin Animation (Last 3 Rows)")]
    public float spinDuration = 0.4f;
    public Vector3 spinAngle = new Vector3(360, 0, 0);
    public Ease spinEase = Ease.OutBack;

    public AudioClip jumpingToGrid, flying, shifting;

    public ParticleSystem effect, confettiEffect;
    private int? cachedColumns = null;
    // Tracks words that have left the board but haven't reached the UI yet
    private Dictionary<string, int> flyingWordsPerCategory = new Dictionary<string, int>();

    private struct GravityMoveInfo
    {
        public Transform block;
        public Vector2Int oldPos;
        public Vector2Int newPos;
        public Transform targetSlot;
        public string wordKey;
        public bool shouldSpin;
        public Vector3 originalLocalScale;
    }

    private struct ArcBlockData
    {
        public List<Transform> elements;
        public Vector3 originalWorldPos;
        public string category;
    }

    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        Taptic.tapticOn = true;
        Application.targetFrameRate = 60;
        StartCoroutine(InitializeDictionary());
    }

    private IEnumerator InitializeDictionary()
    {
        yield return null;
        if (!dictionarySeparated)
        {
            SeparateDictionaryWords();
            dictionarySeparated = true;
        }
        UpdateLastThreeRowsColliders();
    }

    public void Check(int columns)
    {
        cachedColumns = columns;
        if (!isProcessing)
        {
            StartCoroutine(ProcessDestructionAndGravity(columns));
        }
    }

    public void UpdateLastThreeRowsColliders()
    {
        var grid = TopGridManager.instance;
        if (grid == null && !cachedColumns.HasValue) return;

        int columns = grid != null ? grid.columns : cachedColumns.Value;
        if (columns <= 0 || transform.childCount == 0) return;

        int totalRows = transform.childCount / columns;
        int startRow = Mathf.Max(0, totalRows - 3);

        for (int r = 0; r < totalRows; r++)
        {
            bool inLastThreeRows = (r >= startRow);
            for (int c = 0; c < columns; c++)
            {
                int linearIndex = r * columns + c;
                if (linearIndex >= transform.childCount) continue;

                Transform slot = transform.GetChild(linearIndex);
                for (int i = 1; i < slot.childCount; i++)
                {
                    Transform block = slot.GetChild(i);
                    BoxCollider col = block.GetComponent<BoxCollider>();
                    if (col == null) col = block.GetComponentInChildren<BoxCollider>();

                    if (col != null)
                    {
                        col.enabled = inLastThreeRows;
                    }
                }
            }
        }
    }

    private void SeparateDictionaryWords()
    {
        var lvlManager = LevelManager.Instance;
        if (lvlManager == null || lvlManager.wordPositions == null) return;

        var separatedWords = new Dictionary<string, List<Vector2Int>>();
        int idCounter = 0;

        foreach (var kvp in lvlManager.wordPositions)
        {
            List<List<Vector2Int>> grouped = GroupContiguous(kvp.Value);
            foreach (var group in grouped)
            {
                string uniqueKey = kvp.Key + "_" + idCounter;
                bool isAlreadyComplete = true;
                foreach (var pos in group)
                {
                    if (lvlManager.excludedChar.Contains(pos))
                    {
                        isAlreadyComplete = false;
                        break;
                    }
                }
                if (!isAlreadyComplete)
                {
                    separatedWords.Add(uniqueKey, group);
                    idCounter++;
                }
            }
        }
        lvlManager.wordPositions = separatedWords;
    }

    private List<List<Vector2Int>> GroupContiguous(List<Vector2Int> positions)
    {
        List<List<Vector2Int>> groups = new List<List<Vector2Int>>();
        List<Vector2Int> unassigned = new List<Vector2Int>(positions);

        while (unassigned.Count > 0)
        {
            List<Vector2Int> currentGroup = new List<Vector2Int>();
            Queue<Vector2Int> queue = new Queue<Vector2Int>();

            queue.Enqueue(unassigned[0]);
            unassigned.RemoveAt(0);

            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                currentGroup.Add(current);

                for (int i = unassigned.Count - 1; i >= 0; i--)
                {
                    if (IsAdjacent(current, unassigned[i]))
                    {
                        queue.Enqueue(unassigned[i]);
                        unassigned.RemoveAt(i);
                    }
                }
            }
            groups.Add(currentGroup);
        }
        return groups;
    }

    private bool IsAdjacent(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) == 1;
    }

    public void AnimateTrayBlockToGrid(Transform block, Transform slotTransform, Vector2Int matchedKey)
    {
        if (jumpingToGrid != null) AudioSource.PlayClipAtPoint(jumpingToGrid, block.position);

        if (block.childCount >= 2)
        {
            Transform secondChild = block.GetChild(1);
            GameObject[] locks = GameObject.FindGameObjectsWithTag("Lock");

            if (locks != null && locks.Length > 0)
            {
                GameObject targetLock = null;
                float maxZ = float.MinValue;

                foreach (GameObject l in locks)
                {
                    if (l.transform.position.z > maxZ)
                    {
                        maxZ = l.transform.position.z;
                        targetLock = l;
                    }
                }

                if (targetLock != null)
                {
                    targetLock.tag = "Untagged";
                    Sequence keySeq = DOTween.Sequence().SetLink(secondChild.gameObject);
                    keySeq.AppendInterval(keyFlightDelay);

                    keySeq.AppendCallback(() =>
                    {
                        if (secondChild == null || targetLock == null) return;
                        secondChild.SetParent(targetLock.transform);
                        Vector3 destination = targetLock.transform.position + keyOffset;
                        float distance = Vector3.Distance(secondChild.position, destination);
                        float duration = keySpeed > 0 ? distance / keySpeed : 0.5f;
                        Vector3 startScale = secondChild.localScale;

                        Sequence flightSeq = DOTween.Sequence();
                        flightSeq.Append(secondChild.DOScale(startScale * 1.3f, 0.15f).SetEase(Ease.OutBack));
                        flightSeq.Append(secondChild.DOJump(destination, keyJumpPower, 1, duration).SetEase(Ease.InOutSine));
                        flightSeq.Join(secondChild.DORotate(new Vector3(0, 0, 90), duration).SetEase(Ease.InOutSine));
                        flightSeq.Join(secondChild.DOScale(startScale * keyTargetScale, duration).SetEase(Ease.InCubic));

                        flightSeq.AppendCallback(() =>
                        {
                            if (secondChild != null)
                            {
                                secondChild.localPosition = keyOffset;
                                secondChild.localEulerAngles = new Vector3(0, 0, 90);
                            }
                            if (targetLock != null)
                            {
                                targetLock.transform.DOPunchScale(Vector3.one * 0.2f, 0.25f, 10, 1f);
                            }
                        });

                        flightSeq.AppendInterval(0.25f);
                        flightSeq.Append(secondChild.DOLocalRotate(new Vector3(0, 0, 180), keyTurnDuration).SetEase(Ease.InOutQuad));
                        flightSeq.Append(targetLock.transform.DOScale(Vector3.zero, lockDestroyDuration).SetEase(Ease.InBack));

                        flightSeq.OnComplete(() =>
                        {
                            if (targetLock != null) Destroy(targetLock);
                        });
                    });
                }
            }
        }

        block.SetParent(slotTransform.parent);
        MeshRenderer blockRenderer = block.GetComponent<MeshRenderer>();
        MeshRenderer slotRenderer = slotTransform.GetComponent<MeshRenderer>();

        if (blockRenderer != null && slotRenderer != null)
        {
            Color targetColor = slotRenderer.material.color;
            blockRenderer.material.DOColor(targetColor, trayJumpDuration / 2).SetEase(Ease.InOutQuad).SetLink(block.gameObject);
            blockRenderer.material.SetColor("_RimColor", slotRenderer.material.GetColor("_RimColor"));
            blockRenderer.material.SetColor("_ShineColor", slotRenderer.material.GetColor("_ShineColor"));
            blockRenderer.material.SetFloat("_ShineSize", slotRenderer.material.GetFloat("_ShineSize"));
            blockRenderer.material.SetFloat("_ShineSoftness", slotRenderer.material.GetFloat("_ShineSoftness"));
            blockRenderer.material.SetFloat("_ShineAngle", slotRenderer.material.GetFloat("_ShineAngle"));
            blockRenderer.material.SetFloat("_FresnelPower", slotRenderer.material.GetFloat("_FresnelPower"));
            if (slotRenderer.materials.Length > 1)
            {
                slotRenderer.materials[1].DOColor(Color.green, trayJumpDuration / 2f).SetEase(Ease.InOutBack);
            }
        }

        DOVirtual.DelayedCall(trayJumpDuration - 0.15f, () =>
        {
            if (slotTransform != null)
            {
                slotTransform.DOScale(Vector3.zero, 0.15f)
                             .SetEase(Ease.InBack)
                             .SetLink(slotTransform.gameObject)
                             .OnComplete(() => {
                                 if (slotTransform != null) Destroy(slotTransform.gameObject);
                             });
            }
        });

        block.DOKill();
        Sequence jumpSeq = DOTween.Sequence().SetLink(block.gameObject);
        jumpSeq.Append(block.DOJump(slotTransform.position, trayJumpPower, 1, trayJumpDuration).SetEase(trayJumpEase));

        Vector3 finalScale = new Vector3(.9f, 1f, .9f);
        Sequence scaleSeq = DOTween.Sequence();
        scaleSeq.Append(block.DOScale(finalScale * trayFlightScaleMultiplier, trayJumpDuration * 0.6f).SetEase(Ease.OutSine));
        scaleSeq.Append(block.DOScale(finalScale, trayJumpDuration * 0.4f).SetEase(Ease.InSine));

        jumpSeq.Join(scaleSeq);
        jumpSeq.OnComplete(() =>
        {
            block.localPosition = new Vector3(block.localPosition.x, block.localPosition.y, 0f);
            block.localRotation = Quaternion.identity;
            block.gameObject.layer = LayerMask.NameToLayer("Word");
            FreezeManager.DecreaseFreezeCount();
            reservedGridSlots.Remove(matchedKey);
            Taptic.Heavy();

            var lvlManager = LevelManager.Instance;
            if (lvlManager != null && lvlManager.wordPositions != null)
            {
                string targetWord = null;
                foreach (var kvp in lvlManager.wordPositions)
                {
                    if (kvp.Value.Contains(matchedKey))
                    {
                        targetWord = kvp.Key;
                        if (targetWord.Contains("_")) targetWord = targetWord.Substring(0, targetWord.IndexOf('_'));
                        if (targetWord.Contains("#")) targetWord = targetWord.Substring(0, targetWord.IndexOf('#'));
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(targetWord) && HintManager.instance != null)
                {
                    if (!HintManager.instance.wordChain.ContainsKey(targetWord))
                    {
                        HintManager.instance.wordChain[targetWord] = new();
                    }
                    if (!HintManager.instance.wordChain[targetWord].Key.Contains(block.gameObject))
                    {
                        HintManager.instance.wordChain[targetWord].Key.Add(block.gameObject);
                    }
                }
            }

            UpdateLastThreeRowsColliders();
            if (TopGridManager.instance != null) Check(TopGridManager.instance.columns);
        });
    }

    public bool TryFindGridSlotForLetter(string letter, out Transform slotTransform, out Vector2Int matchedKey)
    {
        var grid = TopGridManager.instance;
        var lvlManager = LevelManager.Instance;
        int columns = grid.columns;

        int startRow = Mathf.Max(0, grid.rows - 3);
        int endRow = grid.rows - 1;

        for (int row = startRow; row <= endRow; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                Vector2Int key = new Vector2Int(row, col);

                if (lvlManager.excludedChar.Contains(key) && string.Equals(lvlManager.cellTexts[key].Trim(), letter.Trim(), System.StringComparison.OrdinalIgnoreCase))
                {
                    int index = key.x * columns + key.y;
                    Transform candidateSlot = grid.transform.GetChild(index).GetChild(1);
                    lvlManager.excludedChar.Remove(key);
                    reservedGridSlots.Add(key);
                    slotTransform = candidateSlot;
                    matchedKey = key;
                    return true;
                }
            }
        }

        slotTransform = null;
        matchedKey = default;
        return false;
    }

    private IEnumerator ProcessDestructionAndGravity(int columns)
    {
        isProcessing = true;
        var lvlManager = LevelManager.Instance;
        bool gravityNeeded = false;

        while (true)
        {
            bool boardChangedThisFrame = false;
            List<string> wordsToDestroy = new List<string>();

            foreach (var word in lvlManager.wordPositions.Keys.ToList())
            {
                bool missingLetter = false;
                foreach (var pos in lvlManager.wordPositions[word])
                {
                    if (lvlManager.excludedChar.Contains(pos))
                    {
                        missingLetter = true;
                        break;
                    }
                }

                if (!missingLetter)
                {
                    bool isStable = true;
                    foreach (var pos in lvlManager.wordPositions[word])
                    {
                        if (reservedGridSlots.Contains(pos))
                        {
                            isStable = false;
                            break;
                        }

                        int linearIndex = pos.x * columns + pos.y;
                        if (linearIndex >= 0 && linearIndex < transform.childCount)
                        {
                            var gridChild = transform.GetChild(linearIndex);
                            for (int j = 1; j < gridChild.childCount; j++)
                            {
                                Transform block = gridChild.GetChild(j);
                                if (DOTween.IsTweening(block) || block.localPosition.sqrMagnitude > 0.01f)
                                {
                                    isStable = false;
                                    break;
                                }
                            }
                        }
                        if (!isStable) break;
                    }

                    if (isStable) wordsToDestroy.Add(word);
                }
            }

            if (wordsToDestroy.Count > 0)
            {
                boardChangedThisFrame = true;
                gravityNeeded = true;

                foreach (string word in wordsToDestroy)
                {
                    StartCoroutine(DestroyWordRoutine(word, columns, wordsToDestroy));
                }
            }

            if (!boardChangedThisFrame && activeDestructions == 0 && !HasFlyingBlocks())
            {
                if (gravityNeeded)
                {
                    isShifting = true;
                    yield return StartCoroutine(WaitForGridStability());
                    Sequence gravitySeq = ApplyGravity(columns);
                    if (gravitySeq != null)
                    {
                        yield return gravitySeq.WaitForCompletion();
                    }
                    UpdateLastThreeRowsColliders();
                    isShifting = false;
                    gravityNeeded = false;
                }
                else
                {
                    UpdateLastThreeRowsColliders();
                    break;
                }
            }

            yield return null;
        }

        isProcessing = false;
        isShifting = false;
    }

    private bool HasFlyingBlocks()
    {
        if (reservedGridSlots.Count > 0) return true;

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform slot = transform.GetChild(i);
            if (slot.childCount > 1)
            {
                for (int j = 1; j < slot.childCount; j++)
                {
                    Transform block = slot.GetChild(j);
                    if (DOTween.IsTweening(block) || block.localPosition.sqrMagnitude > 0.01f)
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }

    private IEnumerator DestroyWordRoutine(string word, int columns, List<string> wordsBeingDestroyedThisPass)
    {
        activeDestructions++;
        var lvlManager = LevelManager.Instance;
        List<ArcBlockData> blocksInWord = new List<ArcBlockData>();
        List<GameObject> objectsToDestroy = new List<GameObject>();

        if (lvlManager.wordPositions.ContainsKey(word))
        {
            foreach (var pos in lvlManager.wordPositions[word])
            {
                bool stillNeeded = IsPositionStillNeeded(pos, word, lvlManager.wordPositions, wordsBeingDestroyedThisPass, out _);
                if (!stillNeeded)
                {
                    int linearIndex = pos.x * columns + pos.y;
                    if (linearIndex >= 0 && linearIndex < transform.childCount)
                    {
                        var gridChild = transform.GetChild(linearIndex);
                        if (gridChild.childCount > 1)
                        {
                            string foundCategory = "";
                            if (lvlManager.cellCategory.TryGetValue(pos, out string cat)) foundCategory = cat;

                            ArcBlockData blockData = new ArcBlockData
                            {
                                elements = new List<Transform>(),
                                originalWorldPos = gridChild.position,
                                category = foundCategory
                            };

                            while (gridChild.childCount > 1)
                            {
                                Transform child = gridChild.GetChild(1);
                                child.DOKill();
                                child.SetParent(null);
                                blockData.elements.Add(child);
                                objectsToDestroy.Add(child.gameObject);
                            }
                            blocksInWord.Add(blockData);
                        }
                    }
                    lvlManager.excludedChar.Remove(pos);
                    lvlManager.cellCategory.Remove(pos);
                    lvlManager.cellTexts.Remove(pos);
                }
            }
            lvlManager.wordPositions.Remove(word);
        }

        Sequence destroySeq = DOTween.Sequence();
        if (blocksInWord.Count > 0)
        {
            blocksInWord = blocksInWord.OrderBy(b => b.originalWorldPos.x).ThenByDescending(b => b.originalWorldPos.y).ToList();
            Vector3 centerPos = Vector3.zero;
            foreach (var b in blocksInWord) centerPos += b.originalWorldPos;
            centerPos /= blocksInWord.Count;

            if (effect != null)
            {
                Color targetColor = Color.white;
                if (blocksInWord.Count > 0 && blocksInWord[0].elements.Count > 0)
                {
                    MeshRenderer mr = blocksInWord[0].elements[0].GetComponentInChildren<MeshRenderer>();
                    if (mr != null) targetColor = mr.material.color;
                }

                Vector3 effectPos = centerPos + new Vector3(0f, arcHeightOffset, -1.4f);
                ParticleSystem spawnedEffect = Instantiate(effect, effectPos, Quaternion.identity);

                ParticleSystem[] allParticleSystems = spawnedEffect.GetComponentsInChildren<ParticleSystem>();
                foreach (ParticleSystem ps in allParticleSystems)
                {
                    var mainModule = ps.main;
                    mainModule.startColor = targetColor;
                }

                spawnedEffect.Play();
                Destroy(spawnedEffect.gameObject, spawnedEffect.main.duration + spawnedEffect.main.startLifetime.constantMax);
            }

            int blockCount = blocksInWord.Count;
            float centerIndex = (blockCount - 1) / 2f;
            string wordCategoryTarget = "";

            foreach (var b in blocksInWord)
            {
                if (!string.IsNullOrEmpty(b.category))
                {
                    wordCategoryTarget = b.category;
                    break;
                }
            }

            if (lvlManager.wordsCategory != null && !string.IsNullOrEmpty(wordCategoryTarget))
            {
                string dictKey = lvlManager.wordsCategory.Keys.FirstOrDefault(k => k.Trim().Equals(wordCategoryTarget.Trim(), System.StringComparison.OrdinalIgnoreCase));
                if (dictKey != null)
                {
                    string baseWord = word;
                    if (baseWord.Contains("_")) baseWord = baseWord.Substring(0, baseWord.IndexOf('_'));
                    if (baseWord.Contains("#")) baseWord = baseWord.Substring(0, baseWord.IndexOf('#'));

                    var wordList = lvlManager.wordsCategory[dictKey];
                    string matchedItem = wordList.FirstOrDefault(w => w.Trim().Equals(baseWord.Trim(), System.StringComparison.OrdinalIgnoreCase));
                    if (matchedItem != null) wordList.Remove(matchedItem);
                }
            }

            Transform wordUITransform = null;
            Vector2 targetScreenPos = Vector2.zero;

            if (categoryUIParent != null && !string.IsNullOrEmpty(wordCategoryTarget))
            {
                string searchCat = wordCategoryTarget.Trim().ToLower().Replace("\n", " ").Replace("\r", "");
                foreach (Transform categoryImage in categoryUIParent)
                {
                    TextMeshProUGUI[] textComps = categoryImage.GetComponentsInChildren<TextMeshProUGUI>(true);
                    foreach (var tmpComp in textComps)
                    {
                        if (tmpComp != null)
                        {
                            string cleanUIText = tmpComp.text.Trim().ToLower().Replace("\n", " ").Replace("\r", "");
                            if (cleanUIText == searchCat)
                            {
                                Canvas canvas = categoryImage.GetComponentInParent<Canvas>();
                                Camera uiCam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;
                                targetScreenPos = RectTransformUtility.WorldToScreenPoint(uiCam, categoryImage.position);
                                wordUITransform = categoryImage;
                                break;
                            }
                        }
                    }
                    if (wordUITransform != null) break;
                }
            }

            for (int i = 0; i < blockCount; i++)
            {
                float idx = i - centerIndex;
                float arcLength = idx * arcSpacingX;
                float theta = arcLength / arcRadius;
                float offsetX = arcRadius * Mathf.Sin(theta);
                float offsetZ = arcRadius * (1f - Mathf.Cos(theta));
                float angleY = -theta * Mathf.Rad2Deg;

                foreach (var child in blocksInWord[i].elements)
                {
                    Vector3 startScale = child.localScale;
                    Vector3 startRot = child.eulerAngles;
                    Vector3 targetPos = new Vector3(centerPos.x + offsetX, centerPos.y + arcHeightOffset, centerPos.z + offsetZ);
                    Vector3 targetRot = new Vector3(startRot.x, startRot.y + angleY, startRot.z);
                    Sequence blockSeq = DOTween.Sequence().SetLink(child.gameObject);

                    blockSeq.Append(child.DOMove(targetPos, popDuration).SetEase(Ease.OutQuad));
                    blockSeq.Join(child.DORotate(targetRot, popDuration).SetEase(Ease.OutQuad));
                    blockSeq.Join(child.DOScale(startScale * arcScaleUp, popDuration).SetEase(Ease.OutBack));

                    blockSeq.AppendCallback(() =>
                    {
                        if (child != null)
                        {
                            MeshRenderer mr = child.GetComponentInChildren<MeshRenderer>();
                            if (mr != null) mr.material.SetFloat("_Enable_Highlights", 1);
                        }
                    });

                    blockSeq.AppendInterval(destructionDelay + (i * flightStaggerDelay));

                    blockSeq.AppendCallback(() =>
                    {
                        if (child != null)
                        {
                            MeshRenderer mr = child.GetComponentInChildren<MeshRenderer>();
                            if (mr != null) mr.material.SetFloat("_Enable_Highlights", 0);
                        }
                    });

                    if (wordUITransform != null)
                    {
                        blockSeq.AppendCallback(() =>
                        {
                            if (flying != null && child != null) AudioSource.PlayClipAtPoint(flying, child.position);
                        });

                        Camera cam = Camera.main;
                        float distanceToCamera = Mathf.Max(0.5f, cam.WorldToScreenPoint(targetPos).z - flightElevationOffset);
                        Vector3 finalWorldPos = cam.ScreenToWorldPoint(new Vector3(targetScreenPos.x, targetScreenPos.y, distanceToCamera));

                        blockSeq.Append(child.DOMove(finalWorldPos, flyToUIDuration).SetEase(flyEase));
                        blockSeq.Join(child.DORotate(flightRotation, flyToUIDuration, RotateMode.FastBeyond360).SetRelative(true).SetEase(flyEase));
                        blockSeq.Join(child.DOScale(Vector3.zero, flyToUIDuration).SetEase(destroyEase));

                        blockSeq.OnComplete(() =>
                        {
                            if (wordUITransform != null)
                            {
                                wordUITransform.DOKill(true);
                                Vector3 punchStrength = uiPopScale - Vector3.one;
                                wordUITransform.DOPunchScale(punchStrength, uiPopDuration, 5, 0.3f).SetLink(wordUITransform.gameObject);
                            }
                        });
                    }
                    else
                    {
                        blockSeq.Append(child.DOScale(Vector3.zero, flyToUIDuration).SetEase(destroyEase));
                    }
                    destroySeq.Insert(0, blockSeq);
                }
            }

            destroySeq.OnComplete(() =>
            {
                if (wordUITransform != null && !string.IsNullOrEmpty(wordCategoryTarget))
                {
                    int remainingCount = 0;
                    if (lvlManager.wordsCategory != null)
                    {
                        foreach (var key in lvlManager.wordsCategory.Keys)
                        {
                            if (key.Trim().Equals(wordCategoryTarget.Trim(), System.StringComparison.OrdinalIgnoreCase))
                            {
                                remainingCount = lvlManager.wordsCategory[key].Count;
                                break;
                            }
                        }
                    }

                    if (remainingCount <= 0)
                    {
                        if (wordUITransform.childCount > 2)
                        {
                            wordUITransform.GetChild(1).gameObject.SetActive(false);
                            Transform checkmarkTransform = wordUITransform.GetChild(2);
                            checkmarkTransform.gameObject.SetActive(true);

                            if (confettiEffect != null)
                            {
                                ParticleSystem confettiInstance = Instantiate(confettiEffect, checkmarkTransform);
                                confettiInstance.transform.localPosition = Vector3.zero;
                                confettiInstance.transform.localRotation = Quaternion.identity;
                                confettiInstance.transform.localScale = Vector3.one * 25;

                                int uiLayer = LayerMask.NameToLayer("UI");
                                ParticleSystem[] allPs = confettiInstance.GetComponentsInChildren<ParticleSystem>(true);
                                foreach (ParticleSystem ps in allPs)
                                {
                                    ps.gameObject.layer = uiLayer;
                                    var main = ps.main;
                                    main.loop = false;
                                    var renderer = ps.GetComponent<ParticleSystemRenderer>();
                                    if (renderer != null)
                                    {
                                        renderer.sortingLayerName = "UI";
                                        renderer.sortingOrder = 100;
                                    }
                                }
                                confettiInstance.Play();
                                Destroy(confettiInstance.gameObject, confettiInstance.main.duration + confettiInstance.main.startLifetime.constantMax);
                            }
                        }
                    }
                    else
                    {
                        if (wordUITransform.childCount > 1)
                        {
                            var tmp = wordUITransform.GetChild(1).GetComponentInChildren<TextMeshProUGUI>();
                            if (tmp != null) tmp.text = remainingCount.ToString();
                        }
                    }
                }
            });
        }

        if (objectsToDestroy.Count > 0)
        {
            yield return destroySeq.WaitForCompletion();
            foreach (var obj in objectsToDestroy)
            {
                if (obj != null) Destroy(obj);
            }
        }
        activeDestructions--;
    }

    private IEnumerator WaitForGridStability()
    {
        bool stable = false;
        float timeout = 5.0f;
        float timer = 0f;

        while (!stable && timer < timeout)
        {
            stable = true;
            if (reservedGridSlots.Count > 0)
            {
                stable = false;
            }
            else
            {
                for (int i = 0; i < transform.childCount; i++)
                {
                    Transform slot = transform.GetChild(i);
                    if (slot.childCount > 1)
                    {
                        for (int j = 1; j < slot.childCount; j++)
                        {
                            Transform block = slot.GetChild(j);
                            if (DOTween.IsTweening(block) || block.localPosition.sqrMagnitude > 0.01f)
                            {
                                stable = false;
                                break;
                            }
                        }
                    }
                    if (!stable) break;
                }
            }

            if (!stable)
            {
                timer += 0.1f;
                yield return new WaitForSeconds(0.1f);
            }
        }

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform slot = transform.GetChild(i);
            for (int j = 1; j < slot.childCount; j++)
            {
                Transform block = slot.GetChild(j);
                if (!DOTween.IsTweening(block))
                {
                    block.localPosition = Vector3.zero;
                }
            }
        }
    }

    private Sequence ApplyGravity(int columns)
    {
        var lvlManager = LevelManager.Instance;
        int maxRow = (transform.childCount / columns) - 1;
        int thresholdRow = Mathf.Max(0, maxRow - 2);

        List<GravityMoveInfo> moves = new List<GravityMoveInfo>();
        List<KeyValuePair<Vector2Int, Vector2Int>> logicalMoves = new List<KeyValuePair<Vector2Int, Vector2Int>>();
        int emptyRowsBelow = 0;

        for (int r = maxRow; r >= 0; r--)
        {
            if (IsRowEmpty(r, columns))
            {
                emptyRowsBelow++;
            }
            else if (emptyRowsBelow > 0)
            {
                int targetRow = r + emptyRowsBelow;

                for (int c = 0; c < columns; c++)
                {
                    int currentLinearIndex = r * columns + c;
                    if (currentLinearIndex >= transform.childCount) continue;

                    Transform currentSlot = transform.GetChild(currentLinearIndex);
                    if (currentSlot.childCount > 1)
                    {
                        int targetLinearIndex = targetRow * columns + c;
                        Transform targetSlot = transform.GetChild(targetLinearIndex);

                        Vector2Int oldPos = new Vector2Int(r, c);
                        Vector2Int newPos = new Vector2Int(targetRow, c);

                        logicalMoves.Add(new KeyValuePair<Vector2Int, Vector2Int>(oldPos, newPos));

                        string foundWordKey = null;
                        foreach (var kvp in lvlManager.wordPositions)
                        {
                            if (kvp.Value.Contains(oldPos))
                            {
                                foundWordKey = kvp.Key;
                                break;
                            }
                        }

                        bool isExcludedChar = lvlManager.excludedChar.Contains(oldPos);
                        bool startedAboveThreshold = oldPos.x < thresholdRow;
                        bool landedInsideThreshold = targetRow >= thresholdRow;
                        bool justCrossedIntoLastThree = startedAboveThreshold && landedInsideThreshold;

                        List<Transform> childrenToMove = new List<Transform>();
                        for (int i = 1; i < currentSlot.childCount; i++)
                        {
                            childrenToMove.Add(currentSlot.GetChild(i));
                        }

                        foreach (Transform block in childrenToMove)
                        {
                            moves.Add(new GravityMoveInfo
                            {
                                block = block,
                                oldPos = oldPos,
                                newPos = newPos,
                                targetSlot = targetSlot,
                                wordKey = foundWordKey ?? $"isolated_{r}_{c}",
                                shouldSpin = isExcludedChar && justCrossedIntoLastThree,
                                originalLocalScale = block.localScale
                            });
                        }
                    }
                }
            }
        }

        if (moves.Count == 0 && logicalMoves.Count == 0) return null;

        if (shifting != null)
        {
            Vector3 soundPos = Camera.main != null ? Camera.main.transform.position : transform.position;
            AudioSource.PlayClipAtPoint(shifting, soundPos);
            Taptic.Heavy();
        }

        foreach (var logicMove in logicalMoves)
        {
            MoveCellLogicData(logicMove.Key, logicMove.Value, lvlManager);
        }

        Sequence masterGravitySeq = DOTween.Sequence().SetLink(gameObject);
        var groupedMoves = moves.GroupBy(m => m.wordKey);

        foreach (var group in groupedMoves)
        {
            string wordKey = group.Key;
            List<GravityMoveInfo> wordMoves = group.ToList();

            Vector3 startCenter = Vector3.zero;
            Vector3 targetCenter = Vector3.zero;

            foreach (var m in wordMoves)
            {
                startCenter += m.block.position;
                targetCenter += m.targetSlot.position;
            }
            startCenter /= wordMoves.Count;
            targetCenter /= wordMoves.Count;

            GameObject pivotGo = new GameObject("Pivot_" + wordKey);
            Transform pivot = pivotGo.transform;
            pivot.position = startCenter;

            foreach (var m in wordMoves)
            {
                m.block.SetParent(pivot, true);
            }

            Sequence wordSeq = DOTween.Sequence().SetLink(pivotGo);
            wordSeq.Append(pivot.DOScale(gravityShrinkScale, gravityShrinkDuration).SetEase(Ease.InOutQuad));
            wordSeq.Append(pivot.DOJump(targetCenter, gravityJumpPower, 1, gravityJumpDuration).SetEase(Ease.OutQuad));
            wordSeq.Append(pivot.DOScale(1f, gravityGrowDuration).SetEase(Ease.OutBack));

            wordSeq.OnComplete(() =>
            {
                foreach (var m in wordMoves)
                {
                    if (m.block != null && m.targetSlot != null)
                    {
                        m.block.SetParent(m.targetSlot);
                        m.block.localPosition = Vector3.zero;
                        m.block.localRotation = Quaternion.identity;
                        m.block.localScale = m.originalLocalScale;

                        if (m.shouldSpin)
                        {
                            m.block.DORotate(spinAngle, spinDuration, RotateMode.FastBeyond360).SetRelative(true).SetEase(spinEase).SetLink(m.block.gameObject);
                            MeshRenderer mr = m.block.GetComponentInChildren<MeshRenderer>();
                            if (mr != null && mr.materials.Length > 1)
                            {
                                mr.materials[1].DOColor(Color.white, spinDuration).SetEase(spinEase).SetLink(m.block.gameObject);
                            }
                        }
                    }
                }
                Destroy(pivotGo);
                UpdateLastThreeRowsColliders();
            });

            masterGravitySeq.Insert(0, wordSeq);
        }

        return masterGravitySeq;
    }

    private bool IsRowEmpty(int row, int columns)
    {
        for (int c = 0; c < columns; c++)
        {
            int linearIndex = row * columns + c;
            if (linearIndex >= transform.childCount) continue;
            if (transform.GetChild(linearIndex).childCount > 1) return false;
        }
        return true;
    }

    private void MoveCellLogicData(Vector2Int oldPos, Vector2Int newPos, LevelManager lvlManager)
    {
        if (lvlManager.excludedChar.Contains(oldPos))
        {
            lvlManager.excludedChar.Remove(oldPos);
            lvlManager.excludedChar.Add(newPos);
        }
        MoveDictionaryEntry(lvlManager.cellCategory, oldPos, newPos);
        MoveDictionaryEntry(lvlManager.cellTexts, oldPos, newPos);

        foreach (var word in lvlManager.wordPositions.Keys.ToList())
        {
            var posList = lvlManager.wordPositions[word];
            if (posList == null) continue;

            bool listChanged = false;
            for (int i = 0; i < posList.Count; i++)
            {
                if (posList[i] == oldPos)
                {
                    posList[i] = newPos;
                    listChanged = true;
                }
            }
            if (listChanged) lvlManager.wordPositions[word] = posList;
        }
    }

    private void MoveDictionaryEntry<T>(Dictionary<Vector2Int, T> dict, Vector2Int oldPos, Vector2Int newPos)
    {
        if (dict.ContainsKey(oldPos))
        {
            var val = dict[oldPos];
            dict.Remove(oldPos);
            dict[newPos] = val;
        }
    }

    private bool IsPositionStillNeeded(Vector2Int pos, string wordBeingDestroyed, Dictionary<string, List<Vector2Int>> wordPositions, List<string> wordsBeingDestroyedThisPass, out string blockingWord)
    {
        blockingWord = null;
        foreach (var kvp in wordPositions)
        {
            if (kvp.Key == wordBeingDestroyed || wordsBeingDestroyedThisPass.Contains(kvp.Key)) continue;
            if (kvp.Value.Contains(pos))
            {
                blockingWord = kvp.Key;
                return true;
            }
        }
        return false;
    }

    private void SilentlyDestroyWord(string word, int columns)
    {
        var lvlManager = LevelManager.Instance;
        if (!lvlManager.wordPositions.ContainsKey(word)) return;

        string wordCategoryTarget = "";
        foreach (var pos in lvlManager.wordPositions[word])
        {
            if (lvlManager.cellCategory.TryGetValue(pos, out string cat))
            {
                wordCategoryTarget = cat;
                break;
            }
        }

        string baseWord = word;
        if (baseWord.Contains("_")) baseWord = baseWord.Substring(0, baseWord.IndexOf('_'));
        if (baseWord.Contains("#")) baseWord = baseWord.Substring(0, baseWord.IndexOf('#'));

        foreach (var pos in lvlManager.wordPositions[word])
        {
            int linearIndex = pos.x * columns + pos.y;
            if (linearIndex >= 0 && linearIndex < transform.childCount)
            {
                var gridChild = transform.GetChild(linearIndex);
                while (gridChild.childCount > 1)
                {
                    Transform child = gridChild.GetChild(1);
                    child.DOKill();
                    child.SetParent(null); // FIXED INFINITE LOOP
                    Destroy(child.gameObject);
                }
            }
            lvlManager.excludedChar.Remove(pos);
            lvlManager.cellCategory.Remove(pos);
            lvlManager.cellTexts.Remove(pos);
        }
        lvlManager.wordPositions.Remove(word);

        int remainingCount = 0;
        if (lvlManager.wordsCategory != null && !string.IsNullOrEmpty(wordCategoryTarget))
        {
            string dictKey = lvlManager.wordsCategory.Keys.FirstOrDefault(k => k.Trim().Equals(wordCategoryTarget.Trim(), System.StringComparison.OrdinalIgnoreCase));
            if (dictKey != null)
            {
                var wordList = lvlManager.wordsCategory[dictKey];
                string matchedItem = wordList.FirstOrDefault(w => w.Trim().Equals(baseWord.Trim(), System.StringComparison.OrdinalIgnoreCase));
                if (matchedItem != null) wordList.Remove(matchedItem);
                remainingCount = wordList.Count;
            }
        }

        if (categoryUIParent != null && !string.IsNullOrEmpty(wordCategoryTarget))
        {
            string searchCat = wordCategoryTarget.Trim().ToLower().Replace("\n", " ").Replace("\r", "");
            Transform wordUITransform = null;

            foreach (Transform categoryImage in categoryUIParent)
            {
                TextMeshProUGUI[] textComps = categoryImage.GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach (var tmpComp in textComps)
                {
                    if (tmpComp != null && tmpComp.text.Trim().ToLower().Replace("\n", " ").Replace("\r", "") == searchCat)
                    {
                        wordUITransform = categoryImage;
                        break;
                    }
                }
                if (wordUITransform != null) break;
            }

            if (wordUITransform != null)
            {
                if (remainingCount <= 0)
                {
                    if (wordUITransform.childCount > 2)
                    {
                        wordUITransform.GetChild(1).gameObject.SetActive(false);
                        Transform checkmarkTransform = wordUITransform.GetChild(2);
                        checkmarkTransform.gameObject.SetActive(true);

                        if (confettiEffect != null)
                        {
                            ParticleSystem confettiInstance = Instantiate(confettiEffect, checkmarkTransform);
                            confettiInstance.transform.localPosition = Vector3.zero;
                            confettiInstance.transform.localRotation = Quaternion.identity;
                            confettiInstance.transform.localScale = Vector3.one * 25;

                            int uiLayer = LayerMask.NameToLayer("UI");
                            ParticleSystem[] allPs = confettiInstance.GetComponentsInChildren<ParticleSystem>(true);
                            foreach (ParticleSystem ps in allPs)
                            {
                                ps.gameObject.layer = uiLayer;
                                var main = ps.main;
                                main.loop = false;
                                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                                if (renderer != null)
                                {
                                    renderer.sortingLayerName = "UI";
                                    renderer.sortingOrder = 100;
                                }
                            }
                            confettiInstance.Play();
                            Destroy(confettiInstance.gameObject, confettiInstance.main.duration + confettiInstance.main.startLifetime.constantMax);
                        }
                    }
                }
                else
                {
                    if (wordUITransform.childCount > 1)
                    {
                        var tmp = wordUITransform.GetChild(1).GetComponentInChildren<TextMeshProUGUI>();
                        if (tmp != null) tmp.text = remainingCount.ToString();
                    }
                }
            }
        }
    }

    public bool TryPlaceVacuumedPiece(Transform blockTransform)
    {
        var grid = TopGridManager.instance;
        var lvlManager = LevelManager.Instance;
        int columns = grid != null ? grid.columns : (cachedColumns ?? 0);

        if (columns <= 0 || transform.childCount == 0) return false;
        int totalRows = transform.childCount / columns;

        // --- NEW SNIPPET ---
        // 1. Force the block itself to be active (in case it was a hidden nested letter)
        blockTransform.gameObject.SetActive(true);

        // 2. Find the TextMeshPro, including inactive children just in case
        TextMeshPro tmp = blockTransform.GetComponentInChildren<TextMeshPro>(true);
        if (tmp == null) return false;

        // 3. Ensure the text object itself is also active
        tmp.gameObject.SetActive(true);
        // -------------------

        // Trim for safety against hidden whitespace
        string letter = tmp.text.Trim();

        Vector2Int matchedKey = new Vector2Int(-1, -1);
        bool found = false;

        // 1. Search ANYWHERE in the grid (Bottom-Up)
        for (int row = totalRows - 1; row >= 0; row--)
        {
            for (int col = 0; col < columns; col++)
            {
                Vector2Int key = new Vector2Int(row, col);

                // Compare letters cleanly ignoring case and spaces
                if (lvlManager.excludedChar.Contains(key) && lvlManager.cellTexts.ContainsKey(key))
                {
                    if (string.Equals(lvlManager.cellTexts[key].Trim(), letter, System.StringComparison.OrdinalIgnoreCase))
                    {
                        if (!reservedGridSlots.Contains(key))
                        {
                            matchedKey = key;
                            found = true;
                            break;
                        }
                    }
                }
            }
            if (found) break;
        }

        if (!found) return false;

        int index = matchedKey.x * columns + matchedKey.y;
        if (index >= transform.childCount) return false;

        Transform slotTransform = transform.GetChild(index);
        Transform placeholder = null;

        if (slotTransform.childCount > 1)
        {
            placeholder = slotTransform.GetChild(1);
        }

        // 2. Inherit Material Properties safely (even if mesh renderer is nested)
        MeshRenderer blockRenderer = blockTransform.GetComponent<MeshRenderer>();
        if (blockRenderer == null) blockRenderer = blockTransform.GetComponentInChildren<MeshRenderer>();

        MeshRenderer slotRenderer = placeholder != null ? placeholder.GetComponent<MeshRenderer>() : null;
        if (slotRenderer == null && placeholder != null) slotRenderer = placeholder.GetComponentInChildren<MeshRenderer>();

        if (blockRenderer != null && slotRenderer != null)
        {
            Color targetColor = slotRenderer.material.color;
            blockRenderer.material.color = targetColor;
            blockRenderer.material.SetColor("_RimColor", slotRenderer.material.GetColor("_RimColor"));
            blockRenderer.material.SetColor("_ShineColor", slotRenderer.material.GetColor("_ShineColor"));
            blockRenderer.material.SetFloat("_ShineSize", slotRenderer.material.GetFloat("_ShineSize"));
            blockRenderer.material.SetFloat("_ShineSoftness", slotRenderer.material.GetFloat("_ShineSoftness"));
            blockRenderer.material.SetFloat("_ShineAngle", slotRenderer.material.GetFloat("_ShineAngle"));
            blockRenderer.material.SetFloat("_FresnelPower", slotRenderer.material.GetFloat("_FresnelPower"));

            if (slotRenderer.materials.Length > 1 && blockRenderer.materials.Length > 1)
            {
                blockRenderer.materials[1].color = Color.green;
            }
        }

        if (placeholder != null)
        {
            placeholder.DOKill();
            Destroy(placeholder.gameObject);
        }

        // 3. Snap block to the grid 
        blockTransform.DOKill();
        blockTransform.SetParent(slotTransform);
        blockTransform.localPosition = Vector3.zero;
        blockTransform.localRotation = Quaternion.identity;
        blockTransform.localScale = new Vector3(0.9f, 1f, 0.9f);
        blockTransform.gameObject.layer = LayerMask.NameToLayer("Word");
        FreezeManager.DecreaseFreezeCount();
        lvlManager.excludedChar.Remove(matchedKey);

        HandlePotentialWordCompletion(matchedKey, columns, totalRows);
        return true;
    }

    private void HandlePotentialWordCompletion(Vector2Int placedPos, int columns, int totalRows)
    {
        var lvlManager = LevelManager.Instance;
        string targetWordKey = null;

        foreach (var kvp in lvlManager.wordPositions)
        {
            if (kvp.Value.Contains(placedPos))
            {
                targetWordKey = kvp.Key;
                break;
            }
        }

        if (string.IsNullOrEmpty(targetWordKey)) return;

        bool isComplete = true;
        foreach (var pos in lvlManager.wordPositions[targetWordKey])
        {
            if (lvlManager.excludedChar.Contains(pos) || reservedGridSlots.Contains(pos))
            {
                isComplete = false;
                break;
            }
        }

        if (isComplete)
        {
            int startRow = Mathf.Max(0, totalRows - 3);
            bool inLastThreeRows = false;

            foreach (var pos in lvlManager.wordPositions[targetWordKey])
            {
                if (pos.x >= startRow)
                {
                    inLastThreeRows = true;
                    break;
                }
            }

            if (!inLastThreeRows)
            {
                SilentlyDestroyWord(targetWordKey, columns);
                if (!isProcessing) Check(columns);
            }
            else
            {
                if (!isProcessing) Check(columns);
            }
        }
    }
}