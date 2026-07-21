using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DG.Tweening;
using TMPro;

public class WordChecker : MonoBehaviour
{
    public static WordChecker instance;
    private bool isProcessing = false;
    private bool dictionarySeparated = false;

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

    private int? cachedColumns = null;

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
    }

    public void Check(int columns)
    {
        cachedColumns = columns;
        if (!isProcessing)
        {
            StartCoroutine(ProcessDestructionAndGravity(columns));
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

    public bool TryFindGridSlotForLetter(string letter, out Transform slotTransform, out Vector2Int matchedKey)
    {
        var grid = TopGridManager.instance;
        var lvlManager = LevelManager.Instance;
        int columns = grid.columns;

        int startRow = grid.rows - 1;
        int endRow = Mathf.Max(0, grid.rows - 3);

        for (int row = startRow; row >= endRow; row--)
        {
            for (int col = 0; col < columns; col++)
            {
                Vector2Int key = new Vector2Int(row, col);

                if (lvlManager.excludedChar.Contains(key) &&
                    !LetterController.reservedGridSlots.Contains(key) &&
                    lvlManager.cellTexts.TryGetValue(key, out string cellLetter) &&
                    cellLetter == letter)
                {
                    int index = key.x * columns + key.y;
                    Transform candidateSlot = grid.transform.GetChild(index);

                    bool slotAlreadyOccupied = false;
                    if (candidateSlot.childCount > 1)
                    {
                        for (int i = 1; i < candidateSlot.childCount; i++)
                        {
                            if (candidateSlot.GetChild(i).GetComponent<LetterController>() != null)
                            {
                                slotAlreadyOccupied = true;
                                break;
                            }
                        }
                    }

                    if (!slotAlreadyOccupied)
                    {
                        slotTransform = candidateSlot;
                        matchedKey = key;
                        return true;
                    }
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
        bool boardChanged = true;

        while (boardChanged)
        {
            boardChanged = false;

            yield return StartCoroutine(WaitForGridStability());

            var lvlManager = LevelManager.Instance;
            List<string> wordsToDestroy = new List<string>();

            foreach (var word in lvlManager.wordPositions.Keys)
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
                if (!missingLetter) wordsToDestroy.Add(word);
            }

            if (wordsToDestroy.Count > 0)
            {
                boardChanged = true;
                Sequence destroySeq = DOTween.Sequence().SetLink(gameObject);
                List<GameObject> objectsToDestroy = new List<GameObject>();

                foreach (string word in wordsToDestroy)
                {
                    if (!lvlManager.wordPositions.ContainsKey(word)) continue;

                    List<ArcBlockData> blocksInWord = new List<ArcBlockData>();

                    foreach (var pos in lvlManager.wordPositions[word])
                    {
                        bool stillNeeded = IsPositionStillNeeded(pos, word, lvlManager.wordPositions, wordsToDestroy, out _);

                        if (!stillNeeded)
                        {
                            int linearIndex = pos.x * columns + pos.y;
                            if (linearIndex >= 0 && linearIndex < transform.childCount)
                            {
                                var gridChild = transform.GetChild(linearIndex);

                                if (gridChild.childCount > 1)
                                {
                                    string foundCategory = "";
                                    if (lvlManager.cellCategory.TryGetValue(pos, out string cat))
                                    {
                                        foundCategory = cat;
                                    }

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
                            lvlManager.charDirection.Remove(pos);
                        }
                    }

                    lvlManager.wordPositions.Remove(word);

                    if (blocksInWord.Count > 0)
                    {
                        blocksInWord = blocksInWord
                            .OrderBy(b => b.originalWorldPos.x)
                            .ThenByDescending(b => b.originalWorldPos.y)
                            .ToList();

                        Vector3 centerPos = Vector3.zero;
                        foreach (var b in blocksInWord) centerPos += b.originalWorldPos;
                        centerPos /= blocksInWord.Count;

                        int blockCount = blocksInWord.Count;
                        float centerIndex = (blockCount - 1) / 2f;

                        string wordCategoryTarget = blocksInWord[0].category;

                        if (lvlManager.wordCategory != null && !string.IsNullOrEmpty(wordCategoryTarget))
                        {
                            string dictKey = lvlManager.wordCategory.Keys.FirstOrDefault(k => k.Trim().Equals(wordCategoryTarget.Trim(), System.StringComparison.OrdinalIgnoreCase));
                            if (dictKey != null)
                            {
                                string baseWord = word;
                                if (baseWord.Contains("_")) baseWord = baseWord.Substring(0, baseWord.IndexOf('_'));
                                if (baseWord.Contains("#")) baseWord = baseWord.Substring(0, baseWord.IndexOf('#'));

                                var wordList = lvlManager.wordCategory[dictKey];
                                string matchedItem = wordList.FirstOrDefault(w => w.Trim().Equals(baseWord.Trim(), System.StringComparison.OrdinalIgnoreCase));
                                if (matchedItem != null)
                                {
                                    wordList.Remove(matchedItem);
                                }
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

                            Vector2 targetScreenPos = Vector2.zero;
                            Transform targetUITransform = null;
                            bool foundUI = false;
                            string targetCategory = blocksInWord[i].category;

                            if (categoryUIParent != null && !string.IsNullOrEmpty(targetCategory))
                            {
                                string searchCat = targetCategory.Trim().ToLower();

                                foreach (Transform categoryImage in categoryUIParent)
                                {
                                    foreach (Transform tChild in categoryImage)
                                    {
                                        var tmpComp = tChild.GetComponent<TextMeshProUGUI>();
                                        if (tmpComp != null)
                                        {
                                            string cleanUIText = tmpComp.text.Trim().ToLower();
                                            if (cleanUIText == searchCat)
                                            {
                                                Canvas canvas = categoryImage.GetComponentInParent<Canvas>();
                                                Camera uiCam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;

                                                targetScreenPos = RectTransformUtility.WorldToScreenPoint(uiCam, categoryImage.position);
                                                targetUITransform = categoryImage;
                                                foundUI = true;
                                                break;
                                            }
                                        }
                                    }
                                    if (foundUI) break;
                                }
                            }

                            bool isLastLetter = (i == blockCount - 1);

                            foreach (var child in blocksInWord[i].elements)
                            {
                                Vector3 startScale = child.localScale;
                                Vector3 startRot = child.eulerAngles;

                                // RESTORED: Smile position matrix calculations restored
                                Vector3 targetPos = new Vector3(centerPos.x + offsetX, centerPos.y + arcHeightOffset, centerPos.z + offsetZ);
                                Vector3 targetRot = new Vector3(startRot.x, startRot.y + angleY, startRot.z);

                                Sequence blockSeq = DOTween.Sequence().SetLink(child.gameObject);

                                // 1. RESTORED: Form inverted smile arc array creation animation smoothly
                                blockSeq.Append(child.DOMove(targetPos, popDuration).SetEase(Ease.OutQuad));
                                blockSeq.Join(child.DORotate(targetRot, popDuration).SetEase(Ease.OutQuad));
                                blockSeq.Join(child.DOScale(startScale * arcScaleUp, popDuration).SetEase(Ease.OutBack));

                                blockSeq.AppendCallback(() =>
                                {
                                    if (child != null)
                                    {
                                        MeshRenderer mr = child.GetComponent<MeshRenderer>();
                                        if (mr != null) mr.material.SetFloat("_Enable_Highlights", 1);
                                    }
                                });

                                // 2. RESTORED: Maintain old staggered visual trail pause inside smile configuration 
                                blockSeq.AppendInterval(destructionDelay + (i * flightStaggerDelay));

                                blockSeq.AppendCallback(() =>
                                {
                                    if (child != null)
                                    {
                                        MeshRenderer mr = child.GetComponent<MeshRenderer>();
                                        if (mr != null) mr.material.SetFloat("_Enable_Highlights", 0);
                                    }
                                });

                                if (foundUI && targetUITransform != null)
                                {
                                    Camera mainCam = Camera.main;
                                    float distanceToCamera = Mathf.Max(0.5f, mainCam.WorldToScreenPoint(targetPos).z - flightElevationOffset);
                                    Vector3 finalWorldPos = mainCam.ScreenToWorldPoint(new Vector3(targetScreenPos.x, targetScreenPos.y, distanceToCamera));

                                    // 3. Fly directly to target panels using the configured project curves
                                    blockSeq.Append(child.DOMove(finalWorldPos, flyToUIDuration).SetEase(flyEase));
                                    blockSeq.Join(child.DORotate(flightRotation, flyToUIDuration, RotateMode.FastBeyond360).SetRelative(true).SetEase(flyEase));
                                    blockSeq.Join(child.DOScale(Vector3.zero, flyToUIDuration).SetEase(destroyEase));

                                    string capturedCategory = targetCategory;

                                    blockSeq.OnComplete(() =>
                                    {
                                        if (targetUITransform != null)
                                        {
                                            // Complete previous scale tracking instantly to stack cleanly
                                            targetUITransform.DOKill(true);

                                            // Keep highly responsive additive squeeze reaction on individual letter impact
                                            Vector3 punchStrength = uiPopScale - Vector3.one;
                                            targetUITransform.DOPunchScale(punchStrength, uiPopDuration, 5, 0.3f)
                                                             .SetLink(targetUITransform.gameObject);

                                            bool categoryHasActiveWordsLeft = false;
                                            if (LevelManager.Instance != null && LevelManager.Instance.wordPositions != null &&
                                                LevelManager.Instance.cellCategory != null && !string.IsNullOrEmpty(capturedCategory))
                                            {
                                                foreach (var remainingWordPositions in LevelManager.Instance.wordPositions.Values)
                                                {
                                                    if (remainingWordPositions != null && remainingWordPositions.Count > 0)
                                                    {
                                                        if (LevelManager.Instance.cellCategory.TryGetValue(remainingWordPositions[0], out string remainingCat))
                                                        {
                                                            if (!string.IsNullOrEmpty(remainingCat) && remainingCat.Trim().Equals(capturedCategory.Trim(), System.StringComparison.OrdinalIgnoreCase))
                                                            {
                                                                categoryHasActiveWordsLeft = true;
                                                                break;
                                                            }
                                                        }
                                                    }
                                                }
                                            }

                                            if (!categoryHasActiveWordsLeft && isLastLetter)
                                            {
                                                if (targetUITransform.childCount > 1)
                                                {
                                                    targetUITransform.GetChild(1).gameObject.SetActive(true);
                                                }
                                            }
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
                    }
                }

                if (objectsToDestroy.Count > 0)
                {
                    yield return destroySeq.WaitForCompletion();
                    foreach (var obj in objectsToDestroy)
                    {
                        if (obj != null) Destroy(obj);
                    }
                }

                yield return StartCoroutine(WaitForGridStability());

                Sequence gravitySeq = ApplyGravity(columns);
                if (gravitySeq != null)
                {
                    yield return gravitySeq.WaitForCompletion();
                }
            }

            if (TryPlaceQueuedCubes(columns))
            {
                boardChanged = true;
            }
        }

        isProcessing = false;
    }

    private IEnumerator WaitForGridStability()
    {
        bool stable = false;
        float timeout = 5.0f;
        float timer = 0f;

        while (!stable && timer < timeout)
        {
            stable = true;

            LetterController[] allCubes = Object.FindObjectsByType<LetterController>(FindObjectsSortMode.None);
            foreach (var cube in allCubes)
            {
                if (cube.currentState == LetterController.LetterState.Finished ||
                    cube.currentState == LetterController.LetterState.Returning ||
                    cube.currentState == LetterController.LetterState.Moving ||
                    cube.currentState == LetterController.LetterState.Aligning)
                {
                    stable = false;
                    break;
                }
            }

            if (!stable)
            {
                timer += 0.1f;
                yield return new WaitForSeconds(0.1f);
                continue;
            }

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
                if (block.GetComponent<LetterController>() != null && !DOTween.IsTweening(block))
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
                            m.block.DORotate(spinAngle, spinDuration, RotateMode.FastBeyond360)
                                   .SetRelative(true)
                                   .SetEase(spinEase)
                                   .SetLink(m.block.gameObject);

                            MeshRenderer mr = m.block.GetComponent<MeshRenderer>();
                            if (mr != null && mr.materials.Length > 1)
                            {
                                mr.materials[1].DOColor(Color.white, spinDuration)
                                               .SetEase(spinEase)
                                               .SetLink(m.block.gameObject);
                            }
                        }
                    }
                }
                Destroy(pivotGo);
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
        MoveDictionaryEntry(lvlManager.charDirection, oldPos, newPos);

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

    private bool TryPlaceQueuedCubes(int columns)
    {
        var grid = TopGridManager.instance;
        bool placedAny = false;

        foreach (Transform queueSlot in grid.queueSlots)
        {
            if (queueSlot.childCount == 0) continue;

            Transform queuedCube = queueSlot.GetChild(queueSlot.childCount - 1);
            LetterController letterController = queuedCube.GetComponent<LetterController>();
            if (letterController == null) continue;

            var textMesh = queuedCube.GetComponentInChildren<TextMeshPro>();
            string cubeLetter = textMesh != null ? textMesh.text : "";

            if (TryFindGridSlotForLetter(cubeLetter, out Transform slot, out Vector2Int key))
            {
                letterController.currentState = LetterController.LetterState.Finished;
                letterController.JumpToTarget();
                placedAny = true;
            }
        }
        return placedAny;
    }
}