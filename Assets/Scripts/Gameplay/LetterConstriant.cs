using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum ConstraintType
{
    Frozen,Hidden,Storage
}
public class LetterConstriant : MonoBehaviour
{
    public ConstraintType constraint = ConstraintType.Frozen;
    [HideInInspector]public int count;
    //Letter Storage
    [HideInInspector] public int slotIndex;
    [HideInInspector] public Vector3 direction;
    [HideInInspector] public string word;

    private TextMeshPro tmp;
    private void Start()
    {
        if(constraint == ConstraintType.Storage)
        {
            tmp = GetComponentInChildren<TextMeshPro>();
           tmp.text = word.Length.ToString();
            PushLetter();
        }
    }
    private void Update()
    {
        if (constraint == ConstraintType.Frozen && count <= 0)
        {
            var lc = GetComponent<LetterController>();
            GetComponent<MeshRenderer>().material = lc.defaultMaterial;
            GetComponentInChildren<TextMeshPro>().text = lc.myLetter;
            Destroy(GetComponent<LetterConstriant>());
        }
    }

    public void DecrememtFreezeCount()
    {
        count--;
        GetComponentInChildren<TextMeshPro>().text = count.ToString();
    }

    public void RevealLetters()
    {
        var lc = GetComponent<LetterController>();
        GetComponent<MeshRenderer>().material = lc.defaultMaterial;
        GetComponentInChildren<TextMeshPro>().text =lc.myLetter;
        foreach(var letter in LevelManager.Instance.chainedLetters[lc.key])
        {
            var index = letter.x*LetterController.column+letter.y;
            var linkedLetter = BottomGridManager.Instance.transform.GetChild(index).GetChild(0).gameObject;
            var linkedLetterLc = linkedLetter.GetComponent<LetterController>();
            linkedLetter.GetComponent<MeshRenderer>().material = linkedLetterLc.defaultMaterial;
            linkedLetter.GetComponentInChildren<TextMeshPro>().text = linkedLetterLc.myLetter;
            Destroy(linkedLetter.GetComponent<LetterConstriant>());
        }
        Destroy(GetComponent<LetterConstriant>());
    }

    public void PushLetter()
    {
        if(slotIndex>= transform.parent.parent.childCount || slotIndex < 0)
        {
            Debug.LogWarning("Invalid Direction "+slotIndex);
            Debug.LogWarning(transform.parent.childCount);

            return;
        }
        if(transform.parent.parent.GetChild(slotIndex).childCount == 0 && word.Length > 0)
        {
            var letterBox = Instantiate(BottomGridManager.Instance.squareSlot, transform.parent.parent.GetChild(slotIndex));
            letterBox.transform.localPosition = Vector3.zero;
            var lc = letterBox.GetComponent<LetterController>();
            lc.direction = direction;
            letterBox.GetComponentInChildren<TextMeshPro>().text = word[0].ToString();
            word = word.Substring(1);
           tmp.text = word.Length.ToString();
        }
    }
}
