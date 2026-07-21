using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening; // Required for DOTween

public class EffectExpand : MonoBehaviour
{
    [Header("Animation Settings")]
    [Tooltip("How much the object should scale up relative to its starting size.")]
    [SerializeField] private float scaleMultiplier = 2f;
    [SerializeField] private float scaleDuration = 0.5f;
    [SerializeField] private float fadeDuration = 0.5f;

    private void OnEnable()
    {
        transform.SetParent(null);
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer == null)
        {
            Debug.LogWarning("No SpriteRenderer found on " + gameObject.name);
            return;
        }

        // Calculate the target scale by multiplying the current localScale by the multiplier
        Vector3 targetScale = transform.localScale * scaleMultiplier;

        // Initialize a new DOTween sequence
        Sequence effectSequence = DOTween.Sequence();

        // 1. Increase the scale
        effectSequence.Append(transform.DOScale(targetScale, scaleDuration));

        // 2. Fade out the sprite
        effectSequence.Append(spriteRenderer.DOFade(0f, fadeDuration));

        // 3. Destroy the game object when the sequence is completely finished
        effectSequence.OnComplete(() => Destroy(gameObject));
    }
}