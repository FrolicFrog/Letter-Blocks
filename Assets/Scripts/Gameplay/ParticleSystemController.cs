using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParticleSystemController : MonoBehaviour
{
    [Header("Single Particle System Setup")]
    public ParticleSystem singleParticleSystem;
    public float singleReplayDelay = 2f;

    [Header("Sequence List Setup")]
    public List<ParticleSystem> particleSystemList;
    public float sequenceDelay = 1f;

    [Header("Controls")]
    [Tooltip("If true, the coroutines will start automatically when the game plays.")]
    public bool playOnStart = true;
    [Tooltip("If true, stopping the coroutines will also instantly clear any currently visible particles.")]
    public bool clearParticlesOnStop = false;

    // Track the coroutines so we can stop them specifically
    private Coroutine singleRoutine;
    private Coroutine sequenceRoutine;

    void Start()
    {
        if (playOnStart)
        {
            StartAll();
        }
    }

    private void OnEnable()
    {
        StartAll();
    }
    private void OnDisable()
    {
        StopAll();
    }

    public void StartAll()
    {
        StartSingle();
        StartSequence();
    }

    public void StopAll()
    {
        StopSingle();
        StopSequence();
    }

    public void StartSingle()
    {
        if (singleParticleSystem == null) return;

        // Prevent multiple instances of the same coroutine from running
        if (singleRoutine == null)
        {
            singleRoutine = StartCoroutine(PlaySingleParticleRoutine());
        }
    }

    public void StopSingle()
    {
        if (singleRoutine != null)
        {
            StopCoroutine(singleRoutine);
            singleRoutine = null;

            if (clearParticlesOnStop && singleParticleSystem != null)
            {
                singleParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }

    public void StartSequence()
    {
        if (particleSystemList == null || particleSystemList.Count == 0) return;

        if (sequenceRoutine == null)
        {
            sequenceRoutine = StartCoroutine(PlaySequenceRoutine());
        }
    }

    public void StopSequence()
    {
        if (sequenceRoutine != null)
        {
            StopCoroutine(sequenceRoutine);
            sequenceRoutine = null;

            if (clearParticlesOnStop)
            {
                foreach (ParticleSystem ps in particleSystemList)
                {
                    if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }
    }


    private IEnumerator PlaySingleParticleRoutine()
    {
        while (true)
        {
            singleParticleSystem.Play();
            yield return new WaitForSeconds(singleReplayDelay);
        }
    }

    private IEnumerator PlaySequenceRoutine()
    {
        while (true)
        {
            foreach (ParticleSystem ps in particleSystemList)
            {
                if (ps != null)
                {
                    ps.Play();
                }
                yield return new WaitForSeconds(sequenceDelay);
            }
        }
    }
}