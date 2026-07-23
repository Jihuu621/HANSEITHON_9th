using System.Linq;
using UnityEngine;

/// <summary>Chooses the Alpha portrait animation from the current stage timer state.</summary>
[DisallowMultipleComponent]
public sealed class CompanionMoodController : MonoBehaviour
{
    private const float StunThreshold = 1f / 3f;

    private static readonly int IdleState = Animator.StringToHash("Alpha_Idle");
    private static readonly int StunState = Animator.StringToHash("Alpha_Stun");
    private static readonly int FunnyState = Animator.StringToHash("Alpha_Funny");

    private static CompanionMoodController instance;
    private Animator portraitAnimator;
    private int currentState;

    public static CompanionMoodController Ensure(StageManager host)
    {
        if (instance == null)
            instance = host.GetComponent<CompanionMoodController>();
        if (instance == null)
            instance = host.gameObject.AddComponent<CompanionMoodController>();
        return instance;
    }

    private void Awake()
    {
        instance = this;
        ResolveAnimator();
        PlayState(IdleState);
    }

    public void SetTimePressure(float normalizedRemainingTime)
    {
        PlayState(normalizedRemainingTime <= StunThreshold ? StunState : IdleState);
    }

    public void PlayClearMood()
    {
        PlayState(FunnyState);
    }

    private void PlayState(int stateHash)
    {
        ResolveAnimator();
        if (portraitAnimator == null || currentState == stateHash)
            return;

        if (!portraitAnimator.HasState(0, stateHash))
        {
            Debug.LogWarning("Alpha Animator is missing a requested mood state.", portraitAnimator);
            return;
        }

        portraitAnimator.Play(stateHash, 0, 0f);
        currentState = stateHash;
    }

    private void ResolveAnimator()
    {
        if (portraitAnimator != null)
            return;

        portraitAnimator = FindObjectsByType<Animator>(FindObjectsInactive.Include)
            .FirstOrDefault(candidate => candidate != null && candidate.name == "Alpha");
    }
}