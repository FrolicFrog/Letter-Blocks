using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using TMPro;
using System.Linq;

public class LetterController : MonoBehaviour
{
    public static List<LetterController> activeOnBelts = new List<LetterController>();
    public static HashSet<Transform> reservedQueueSlots = new HashSet<Transform>();
    public static HashSet<Vector2Int> reservedGridSlots = new HashSet<Vector2Int>();

    public static List<LetterConstriant> frozenLetter,letterStorage; //refresh it from level manager
    [HideInInspector] public List<LetterConstriant> hiddenLetter; //refresh it from level manager

    [HideInInspector] public Dictionary<GameObject, int> linkedLettersParentIndex;
    [HideInInspector] public Vector3 direction;
    [HideInInspector] public Vector2Int key;
    [HideInInspector] public string myLetter;
    public static int column;

    [Header("Movement Tweaks")]
    public float moveSpeed = 5f;
    public float returnSpeed = 10f;
    public float queueSpacing = 0.2f;
    public float beltMoveSpeed = 4f;

    [Header("Jump Settings")]
    public float jumpPower = 2f;
    public float jumpDuration = 1f;
    public float gridPlacedScale = 1.1f;

    [Header("Juice Settings")]
    public float squashFactor = 0.5f;
    public float bulgeFactor = 1.3f;
    public float squashDuration = 0.1f;
    public float restoreDuration = 0.2f;
    public float impactPunchStrength = 0.8f;

    [Header("Other Settings")]
    public float queueScale = 1f;
    public Material defaultMaterial;
    public enum LetterState { Idle, Moving, Returning, Aligning, OnBelt, OnQueue, Finished }
    public LetterState currentState = LetterState.Idle;

    private Transform gridParent;
    private Transform myOriginalSlot;
    private bool isGroupLeader = false;
    private Vector3 originalScale;

    private Dictionary<GameObject, Tween> activeTweens = new Dictionary<GameObject, Tween>();
    private List<GameObject> completeGroup = new List<GameObject>();
    private TopGridManager topMananger;
    private GameObject target;
    private Transform queue;

    private ConveyorBelt activeBelt;
    private Tween currentMoveTween;
    private int? pendingCheckColumns = null;
    private Vector2Int? pendingGridKey = null;

    private void Start()
    {
        myOriginalSlot = transform.parent;
        if (myOriginalSlot != null)
        {
            gridParent = myOriginalSlot.parent;
        }
        topMananger = TopGridManager.instance;
        originalScale = transform.localScale;
        queue = GameObject.FindWithTag("Queue").transform;
        linkedLettersParentIndex = new Dictionary<GameObject, int>();

        if (LevelManager.Instance != null && LevelManager.Instance.chainedLetters != null && LevelManager.Instance.chainedLetters.ContainsKey(key))
        {
            var linkedLettersCords = LevelManager.Instance.chainedLetters[key];
            foreach (var cords in linkedLettersCords)
            {
                int index = cords.x * column + cords.y;

                if (gridParent != null && index < gridParent.childCount)
                {
                    Transform slotTransform = gridParent.GetChild(index);

                    if (slotTransform.childCount > 0)
                    {
                        GameObject childObj = slotTransform.GetChild(0).gameObject;

                        var childController = childObj.GetComponent<LetterController>();
                        if (childController != null)
                        {
                            childController.direction = this.direction;
                        }

                        linkedLettersParentIndex[childObj] = index;
                    }
                }
            }
        }

        if (direction == Vector3.left)
        {
            transform.GetChild(2).GetChild(0).gameObject.SetActive(true);
        }
        else if (direction == Vector3.right)
        {
            transform.GetChild(2).GetChild(2).gameObject.SetActive(true);
        }
        else if (direction == Vector3.forward)
        {
            transform.GetChild(2).GetChild(1).gameObject.SetActive(true);
        }
        else if (direction == Vector3.back)
        {
            transform.GetChild(2).GetChild(3).gameObject.SetActive(true);
        }


        if (frozenLetter == null)
        {
            frozenLetter = new();
            frozenLetter = FindObjectsByType<LetterConstriant>(FindObjectsInactive.Include, FindObjectsSortMode.None).ToList();
            frozenLetter.RemoveAll(letter => letter.constraint != ConstraintType.Frozen);
        }

        if (letterStorage == null)
        {
            letterStorage = new();
            letterStorage = FindObjectsByType<LetterConstriant>(FindObjectsInactive.Include, FindObjectsSortMode.None).ToList();
            letterStorage.RemoveAll(letter => letter.constraint != ConstraintType.Storage);
        }

        if (hiddenLetter == null)
        {

            hiddenLetter = new();
        }
        List<Vector2Int> keys = new();
        keys.Add(key - new Vector2Int(1, 0));
        keys.Add(key + new Vector2Int(1, 0));
        keys.Add(key + new Vector2Int(0, 1));
        keys.Add(key - new Vector2Int(0, 1));




        for (int i = 0; i < keys.Count; i++)
        {

            var index = keys[i].x * column + keys[i].y;
            if (index < BottomGridManager.Instance.transform.childCount && index >= 0)
            {
                if (LevelManager.Instance.hiddenChar.Contains(keys[i]) && LevelManager.Instance.chainedLetters.ContainsKey(keys[i]))
                {
                    hiddenLetter.Add(BottomGridManager.Instance.transform.GetChild(index).GetChild(0).GetComponent<LetterConstriant>());

                }

            }

        }

    }

    private void OnDestroy()
    {
        if (activeOnBelts.Contains(this)) activeOnBelts.Remove(this);
    }

    private void Update()
    {
        if (currentState == LetterState.Idle || currentState == LetterState.Returning) return;

        if (currentState == LetterState.Moving)
        {
            if (!isGroupLeader) return;
            HandleGroupMovement();
        }
        else if (currentState == LetterState.OnBelt)
        {
            HandleIndependentBeltMovement();
        }
    }

    private void HandleGroupMovement()
    {
        bool pathBlocked = false;
        Collider beltCollider = null;

        foreach (GameObject member in completeGroup)
        {
            if (member == null) continue;
            Collider col = member.GetComponent<Collider>();
            if (col == null) continue;

            Vector3 dirNormalized = direction.normalized;
            Vector3 extents = col.bounds.extents;
            float travelExtent = Mathf.Abs(dirNormalized.x * extents.x) + Mathf.Abs(dirNormalized.y * extents.y) + Mathf.Abs(dirNormalized.z * extents.z);
            float checkLength = Mathf.Max(queueSpacing, 0.1f);

            Vector3 checkCenter = member.transform.position + (dirNormalized * (travelExtent + (checkLength / 2f)));
            Vector3 halfExtents = extents * 0.85f;

            if (Mathf.Abs(dirNormalized.x) > 0.5f) halfExtents.x = checkLength / 2f;
            else if (Mathf.Abs(dirNormalized.y) > 0.5f) halfExtents.y = checkLength / 2f;
            else if (Mathf.Abs(dirNormalized.z) > 0.5f) halfExtents.z = checkLength / 2f;

            Collider[] hits = Physics.OverlapBox(checkCenter, halfExtents, member.transform.rotation);
            foreach (Collider hit in hits)
            {
                if (completeGroup.Contains(hit.gameObject)) continue;

                if (hit.CompareTag("Cube") || hit.GetComponent<LetterController>() != null)
                {
                    hit.transform.DOPunchPosition(dirNormalized * impactPunchStrength, squashDuration + restoreDuration, 8, 0.6f);
                    pathBlocked = true;
                    break;
                }

                if (hit.CompareTag("Belt"))
                {
                    beltCollider = hit;
                }
            }
            if (pathBlocked) break;
        }

        if (pathBlocked)
        {
            BounceBack();
            return;
        }

        if (beltCollider != null)
        {
            AlignToBeltAndStart(beltCollider);
            return;
        }

        foreach (GameObject member in completeGroup)
        {
            if (member != null) member.transform.position += direction.normalized * moveSpeed * Time.deltaTime;
        }
    }

    private void AlignToBeltAndStart(Collider beltCollider)
    {
        ConveyorBelt belt = beltCollider.GetComponent<ConveyorBelt>();
        if (belt == null || belt.endPoint == null) return;

        currentState = LetterState.Aligning;

        List<GameObject> sortedGroup = new List<GameObject>(completeGroup);
        sortedGroup.Sort((a, b) =>
        {
            float distA = Vector3.Distance(a.transform.position, belt.endPoint.position);
            float distB = Vector3.Distance(b.transform.position, belt.endPoint.position);
            return distA.CompareTo(distB);
        });

        Vector3 beltCenter = beltCollider.bounds.center;
        Vector3 beltMoveDir = (belt.endPoint.position - beltCenter).normalized;

        float accumulatedDelay = 0f;

        for (int i = 0; i < sortedGroup.Count; i++)
        {
            GameObject member = sortedGroup[i];
            if (member == null) continue;

            var controller = member.GetComponent<LetterController>();
            if (controller == null) continue;

            controller.currentState = LetterState.Aligning;

            Vector3 alignmentPoint = member.transform.position + Vector3.Project(beltCenter - member.transform.position, direction.normalized);
            float alignDist = Vector3.Distance(member.transform.position, alignmentPoint);
            float alignDuration = alignDist / moveSpeed;

            float currentStaggerDelay = accumulatedDelay;
            bool isFirstBlock = (i == 0);

            member.transform.DOMove(alignmentPoint, alignDuration)
                .SetEase(Ease.Linear)
                .SetDelay(currentStaggerDelay)
                .SetLink(member)
                .OnStart(() =>
                {
                    if (controller != null)
                    {
                        controller.SetThirdChildActive(false);
                        controller.SetFourthChildActive(false);
                    }
                })
                .OnComplete(() =>
                {
                    if (controller != null)
                    {
                        controller.currentState = LetterState.OnBelt;
                        controller.activeBelt = belt;
                        controller.isGroupLeader = true;

                        if (!activeOnBelts.Contains(controller))
                            activeOnBelts.Add(controller);
                    }

                    if (isFirstBlock)
                    {
                        OnLoadingBelt();
                    }
                });

            float memberSize = controller.GetSizeAlongAxis(beltMoveDir);
            accumulatedDelay += (memberSize + queueSpacing) / beltMoveSpeed;
        }


    }

    private void HandleIndependentBeltMovement()
    {
        if (activeBelt == null || activeBelt.endPoint == null) return;

        Vector3 targetPos = activeBelt.endPoint.position;
        Vector3 dir = (targetPos - transform.position).normalized;
        float dist = Vector3.Distance(transform.position, targetPos);

        if (dist <= beltMoveSpeed * Time.deltaTime)
        {
            transform.position = targetPos;

            if (activeBelt.nextBelt != null)
            {
                activeBelt = activeBelt.nextBelt;
            }
            else
            {
                currentState = LetterState.Finished;
                if (activeOnBelts.Contains(this)) activeOnBelts.Remove(this);
                JumpToTarget();
            }
            return;
        }

        bool pathBlocked = false;
        foreach (var other in activeOnBelts)
        {
            if (other == this || other == null || other.activeBelt == null) continue;

            if (other.activeBelt == this.activeBelt || other.activeBelt == this.activeBelt.nextBelt)
            {
                Vector3 toOther = other.transform.position - transform.position;

                if (Vector3.Dot(toOther, dir) > 0.05f)
                {
                    float currentGap = toOther.magnitude;
                    float mySize = GetSizeAlongAxis(dir);
                    float otherSize = other.GetSizeAlongAxis(dir);
                    float targetGap = (mySize + otherSize) * 0.5f + queueSpacing;

                    if (currentGap < targetGap)
                    {
                        pathBlocked = true;
                        break;
                    }
                }
            }
        }

        if (!pathBlocked)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPos, beltMoveSpeed * Time.deltaTime);
        }
    }

    public void StartMoving()
    {
        if (currentState != LetterState.Idle) return;

        isGroupLeader = true;
        completeGroup.Clear();
        completeGroup.Add(gameObject);

        foreach (GameObject linkedObj in linkedLettersParentIndex.Keys)
        {
            if (linkedObj != null)
            {
                completeGroup.Add(linkedObj);
                var childCtrl = linkedObj.GetComponent<LetterController>();
                if (childCtrl != null)
                {
                    childCtrl.currentState = LetterState.Moving;
                    childCtrl.isGroupLeader = false;
                }
            }
        }

        currentState = LetterState.Moving;

        foreach (GameObject member in completeGroup)
        {
            if (member != null)
            {
                member.transform.SetParent(null);

                var controller = member.GetComponent<LetterController>();
                if (controller != null)
                {
                    controller.SetThirdChildActive(false);
                }
            }
        }
    }

    public void BounceBack()
    {
        if (currentState == LetterState.Returning || currentState == LetterState.Idle) return;

        currentMoveTween?.Kill();
        if (activeOnBelts.Contains(this)) activeOnBelts.Remove(this);

        foreach (GameObject member in completeGroup)
        {
            if (member != null)
            {
                var controller = member.GetComponent<LetterController>();
                if (controller != null)
                {
                    controller.currentState = LetterState.Returning;
                    if (activeOnBelts.Contains(controller)) activeOnBelts.Remove(controller);
                }
            }
        }

        if (Camera.main != null)
        {
            Camera.main.transform.DOShakePosition(0.2f, 0.3f, 10).SetLink(Camera.main.gameObject);
        }

        Vector3 baseSquashModifiers;
        if (Mathf.Abs(direction.x) > 0.5f) baseSquashModifiers = new Vector3(squashFactor, bulgeFactor, bulgeFactor);
        else if (Mathf.Abs(direction.z) > 0.5f) baseSquashModifiers = new Vector3(bulgeFactor, bulgeFactor, squashFactor);
        else baseSquashModifiers = new Vector3(bulgeFactor, squashFactor, bulgeFactor);

        int completedTweens = 0;
        int totalToTween = completeGroup.Count;

        foreach (GameObject member in completeGroup)
        {
            if (member == null) continue;

            if (activeTweens.TryGetValue(member, out Tween activeTween))
            {
                activeTween?.Kill();
            }

            Transform targetSlot = myOriginalSlot;
            var memberController = member.GetComponent<LetterController>();
            Vector3 targetScale = (memberController != null) ? memberController.originalScale : Vector3.one;

            if (member != gameObject && linkedLettersParentIndex.TryGetValue(member, out int index))
            {
                if (gridParent != null && index < gridParent.childCount)
                {
                    targetSlot = gridParent.GetChild(index);
                }
            }

            if (targetSlot == null) continue;

            float distance = Vector3.Distance(member.transform.position, targetSlot.position);
            float duration = distance / returnSpeed;

            member.transform.SetParent(targetSlot);
            Vector3 individualSquash = Vector3.Scale(targetScale, baseSquashModifiers);

            Sequence returnSeq = DOTween.Sequence().SetLink(member);
            returnSeq.Append(member.transform.DOLocalMove(Vector3.zero, duration).SetEase(Ease.OutQuad));
            returnSeq.Join(member.transform.DOScale(individualSquash, squashDuration).SetEase(Ease.OutQuad));
            returnSeq.Insert(squashDuration, member.transform.DOScale(targetScale, restoreDuration).SetEase(Ease.OutBack));

            returnSeq.OnComplete(() =>
            {
                member.transform.localScale = targetScale;

                if (memberController != null)
                {
                    memberController.SetThirdChildActive(true);
                    memberController.SetFourthChildActive(true);
                    memberController.currentState = LetterState.Idle;
                    memberController.isGroupLeader = false;
                }

                completedTweens++;
                if (completedTweens >= totalToTween)
                {
                    activeTweens.Clear();
                }
            });

            activeTweens[member] = returnSeq;
        }
    }

    public float GetSizeAlongAxis(Vector3 axis)
    {
        Collider col = GetComponent<Collider>();
        if (col == null) return 1.0f;

        Vector3 extents = col.bounds.extents;
        Vector3 absAxis = new Vector3(Mathf.Abs(axis.x), Mathf.Abs(axis.y), Mathf.Abs(axis.z)).normalized;
        return (absAxis.x * extents.x + absAxis.y * extents.y + absAxis.z * extents.z) * 2f;
    }

    private void OnLoadingBelt()
    {
        foreach (var letter in frozenLetter)
        {
            if (letter != null)
            {
                letter.DecrememtFreezeCount();
            }
        }
        
        foreach (var letter in hiddenLetter)
        {
            if (letter != null)
            {
                letter.RevealLetters();
            }
        }

        foreach(var storage in letterStorage)
        {
            storage.PushLetter();
        }
    }

    public void JumpToTarget()
    {
        bool placedOnGrid = false;
        Vector2Int? targetKey = null;

        for (int i = topMananger.rows - 1; i > topMananger.rows - 4; i--)
        {
            for (int j = 0; j < topMananger.columns; j++)
            {
                var checkKey = new Vector2Int(i, j);
                // FIX: Check regular availability AND make sure no other floating letter has claimed it
                if (LevelManager.Instance.excludedChar.Contains(checkKey) &&
                    !reservedGridSlots.Contains(checkKey) &&
                    LevelManager.Instance.cellTexts[checkKey] == GetComponentInChildren<TextMeshPro>().text)
                {
                    var index = checkKey.x * topMananger.columns + checkKey.y;
                    target = topMananger.transform.GetChild(index).gameObject;
                    placedOnGrid = true;
                    targetKey = checkKey;

                    // Claim instantly to prevent race-conditions
                    reservedGridSlots.Add(checkKey);
                    break;
                }
            }
            if (placedOnGrid) break;
        }

        if (!placedOnGrid && currentState != LetterState.OnQueue)
        {
            bool slotAvailable = false;
            for (int i = 0; i < queue.childCount; i++)
            {
                Transform slotTransform = queue.GetChild(i);

                if (slotTransform.childCount == 0 && !reservedQueueSlots.Contains(slotTransform))
                {
                    slotAvailable = true;
                    currentState = LetterState.OnQueue;
                    target = slotTransform.gameObject;

                    reservedQueueSlots.Add(slotTransform);
                    break;
                }
            }
            if (!slotAvailable)
            {
                Debug.Log("Level Failed");
                return;
            }
        }

        if (target != null)
        {
            ExecuteParabolicJump(target.transform, placedOnGrid ? topMananger.columns : (int?)null, targetKey);
        }
    }

    private void ExecuteParabolicJump(Transform targetTransform, int? checkColumns = null, Vector2Int? targetGridKey = null)
    {
        if (checkColumns.HasValue) pendingCheckColumns = checkColumns;
        if (targetGridKey.HasValue) pendingGridKey = targetGridKey;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        SetFourthChildActive(true);

        Transform blockToReplace = null;
        Vector3 finalScale = originalScale * gridPlacedScale;

        if (targetTransform.childCount > 1)
        {
            blockToReplace = targetTransform.GetChild(1);
        }

        if (currentState == LetterState.OnQueue)
        {
            finalScale = Vector3.one * queueScale;
            // Removed: SetFifthChildActive(true); -> Moved to OnComplete below
        }
        else if (blockToReplace != null)
        {
            finalScale = blockToReplace.localScale;
        }

        GameObject objToDestroy = null;
        if (blockToReplace != null)
        {
            MeshRenderer targetMr = blockToReplace.GetComponent<MeshRenderer>();
            MeshRenderer myMr = GetComponent<MeshRenderer>();

            if (targetMr != null && myMr != null)
            {
                int mainMatCount = Mathf.Min(myMr.materials.Length, targetMr.materials.Length);
                for (int i = 0; i < mainMatCount; i++)
                {
                    Color targetColor = targetMr.materials[i].color;
                    myMr.materials[i].DOColor(targetColor, jumpDuration).SetEase(Ease.InOutQuad).SetLink(gameObject);
                }
            }

            if (transform.childCount > 3 && blockToReplace.childCount > 1)
            {
                MeshRenderer myFourthChildMr = transform.GetChild(3).GetComponent<MeshRenderer>();
                MeshRenderer targetSecondChildMr = blockToReplace.GetChild(1).GetComponent<MeshRenderer>();

                if (myFourthChildMr != null && targetSecondChildMr != null)
                {
                    int childMatCount = Mathf.Min(myFourthChildMr.materials.Length, targetSecondChildMr.materials.Length);
                    for (int i = 0; i < childMatCount; i++)
                    {
                        Color targetColor = targetSecondChildMr.materials[i].color;
                        myFourthChildMr.materials[i].DOColor(targetColor, jumpDuration).SetEase(Ease.InOutQuad).SetLink(gameObject);
                    }
                }
            }

            objToDestroy = blockToReplace.gameObject;
        }

        // --- ASYMMETRIC FLUID JUMP TRAJECTORY ---
        Vector3 startPos = transform.position;
        Vector3 endPos = targetTransform.position;

        float distance = Vector3.Distance(new Vector3(startPos.x, 0, startPos.z), new Vector3(endPos.x, 0, endPos.z));
        float dynamicJumpPower = Mathf.Max(jumpPower, distance * 0.3f);
        float peakY = Mathf.Max(startPos.y, endPos.y) + dynamicJumpPower;

        Sequence jumpSeq = DOTween.Sequence().SetLink(gameObject).SetTarget(transform);

        jumpSeq.Insert(0, transform.DOMoveX(endPos.x, jumpDuration).SetEase(Ease.OutCubic));
        jumpSeq.Insert(0, transform.DOMoveZ(endPos.z, jumpDuration).SetEase(Ease.OutCubic));

        Sequence yCurveSeq = DOTween.Sequence();
        yCurveSeq.Append(transform.DOMoveY(peakY, jumpDuration * 0.45f).SetEase(Ease.OutCubic));
        yCurveSeq.Append(transform.DOMoveY(endPos.y, jumpDuration * 0.55f).SetEase(Ease.InQuad));
        jumpSeq.Insert(0, yCurveSeq);

        jumpSeq.Insert(0, transform.DOScale(finalScale, jumpDuration).SetEase(Ease.OutQuad));

        if (objToDestroy != null)
        {
            jumpSeq.InsertCallback(jumpDuration * 0.92f, () =>
            {
                if (objToDestroy != null) Destroy(objToDestroy);
            });
        }

        // 4. Snappy parent anchoring precisely at touchdown frame
        jumpSeq.AppendCallback(() =>
        {
            // FIX: Cache current rotation to prevent resetting it on queue slots
            Quaternion crystalRotation = transform.rotation;

            transform.SetParent(targetTransform);
            transform.localPosition = Vector3.zero;

            if (currentState == LetterState.OnQueue)
            {
                // Maintain its exact current angle rotation inside the queue slot
                transform.rotation = crystalRotation;
            }
            else
            {
                // Align cleanly to the grid coordinates orientation matching standard layout cells
                transform.localRotation = Quaternion.identity;
            }

            transform.localScale = finalScale;

            if (reservedQueueSlots.Contains(targetTransform))
            {
                reservedQueueSlots.Remove(targetTransform);
            }

            if (targetGridKey.HasValue)
            {
                reservedGridSlots.Remove(targetGridKey.Value);
            }

            if (pendingGridKey.HasValue && LevelManager.Instance != null)
            {
                LevelManager.Instance.excludedChar.Remove(pendingGridKey.Value);
                pendingGridKey = null;
            }
        });

        jumpSeq.Append(transform.DOPunchScale(new Vector3(0.15f, -0.18f, 0.15f), 0.15f, 6, 0.4f));

        jumpSeq.OnComplete(() =>
        {
            if (currentState != LetterState.OnQueue)
            {
                currentState = LetterState.Idle;
            }
            else
            {
                // <---- ENABLE 5TH CHILD AFTER JUMP COMPLETES
                SetFifthChildActive(true);
            }

            if (pendingCheckColumns.HasValue && WordChecker.instance != null)
            {
                WordChecker.instance.Check(pendingCheckColumns.Value);
                pendingCheckColumns = null;
            }
        });
    }

    private void SetThirdChildActive(bool active)
    {
        if (transform.childCount > 2)
        {
            transform.GetChild(2).gameObject.SetActive(active);
        }
    }

    private void SetFourthChildActive(bool active)
    {
        if (transform.childCount > 3)
        {
            transform.GetChild(3).gameObject.SetActive(active);
        }
    }

    private void SetFifthChildActive(bool active)
    {
        if (transform.childCount > 4)
        {
            transform.GetChild(4).gameObject.SetActive(active);
        }
    }

  
}