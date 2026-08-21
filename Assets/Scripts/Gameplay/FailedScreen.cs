using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PopUpScaler : MonoBehaviour
{
    private Vector3 originalScale;

    // Define the duration so both animations sync perfectly
    private float scaleDuration = 0.4f;
    private void OnEnable()
    {originalScale = transform.localScale;
        transform.DOKill();
        transform.localScale = Vector3.zero;
        transform.DOScale(originalScale, scaleDuration).SetEase(Ease.OutBack);
       
    }
}
