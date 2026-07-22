using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controls vertical stage progression inside one scene. Each entry opens its own exit,
/// then arms the timer for the next stage; camera movement remains the responsibility of RoomTrigger/Cinemachine.
/// </summary>
[DisallowMultipleComponent]
public sealed class StageManager : MonoBehaviour
{
    [Serializable]
    public sealed class StageDefinition
    {
        [Tooltip("Inspector label only.")]
        public string stageName = "Stage";
        public LightGoalButton mainLightButton;
        public LightGoalButton subLightButton;
        public StageCeiling exitGate;
        public StageTimer timer;
    }

    public static StageManager Instance { get; private set; }

    [Header("Vertical Stage Sequence")]
    [SerializeField] private List<StageDefinition> stages = new();
    [SerializeField] private bool startFirstStageOnStart = true;

    [Header("Legacy Single-Stage References")]
    [Tooltip("Used only when the Stages list is empty.")]
    public StageCeiling ceiling;
    [Tooltip("Used only when the Stages list is empty.")]
    public StageTimer timer;
    [SerializeField] private LightGoalButton mainLightButton;
    [SerializeField] private LightGoalButton subLightButton;

    public int CurrentStageIndex { get; private set; }
    public bool IsClear { get; private set; }
    public bool HasNextStage => HasStageSequence && CurrentStageIndex + 1 < stages.Count;

    public event Action<int> StageCompleted;
    public event Action<int> StageFailed;

    private bool HasStageSequence => stages != null && stages.Count > 0;
    private StageDefinition CurrentStage => HasStageSequence ? stages[CurrentStageIndex] : null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple StageManagers were found. The newest instance will be used.", this);
        }

        Instance = this;
    }

    private void Start()
    {
        if (startFirstStageOnStart)
            BeginStage(0);
    }

    private void OnDestroy()
    {
        UnsubscribeAllTimers();
        if (Instance == this)
            Instance = null;
    }

    /// <summary>Called by either goal button when its light-contact state changes.</summary>
    public void NotifyGoalButtonChanged(LightGoalButton changedButton)
    {
        if (changedButton == null || IsClear)
            return;

        GetCurrentGoals(out LightGoalButton mainGoal, out LightGoalButton subGoal);
        if (changedButton != mainGoal && changedButton != subGoal)
            return;

        TryCompleteCurrentStage();
    }

    /// <summary>Public manual hook for cutscenes or non-button objectives.</summary>
    public void PuzzleComplete()
    {
        TryCompleteCurrentStage(force: true);
    }

    public void BeginStage(int stageIndex)
    {
        int stageCount = HasStageSequence ? stages.Count : 1;
        if (stageIndex < 0 || stageIndex >= stageCount)
        {
            Debug.LogWarning($"Stage index {stageIndex} does not exist.", this);
            return;
        }

        UnsubscribeAllTimers();
        CurrentStageIndex = stageIndex;
        IsClear = false;

        StageTimer activeTimer = GetCurrentTimer();
        if (activeTimer != null)
        {
            activeTimer.OnTimeOver += HandleCurrentStageTimeOver;
            activeTimer.StartTimer();
        }

        TryCompleteCurrentStage();
    }

    public void RestartCurrentStage()
    {
        BeginStage(CurrentStageIndex);
    }

    private void TryCompleteCurrentStage(bool force = false)
    {
        if (IsClear)
            return;

        GetCurrentGoals(out LightGoalButton mainGoal, out LightGoalButton subGoal);
        bool bothGoalsPressed = mainGoal != null
            && subGoal != null
            && mainGoal.IsPressed
            && subGoal.IsPressed;

        if (!force && !bothGoalsPressed)
            return;

        IsClear = true;
        StageTimer activeTimer = GetCurrentTimer();
        if (activeTimer != null)
            activeTimer.StopTimer();

        StageCeiling exitGate = GetCurrentExitGate();
        if (exitGate != null)
            exitGate.Open();

        StageCompleted?.Invoke(CurrentStageIndex);
        Debug.Log($"Stage {CurrentStageIndex + 1} cleared.", this);

        if (HasNextStage)
            BeginStage(CurrentStageIndex + 1);
    }

    private void HandleCurrentStageTimeOver()
    {
        if (IsClear)
            return;

        StageFailed?.Invoke(CurrentStageIndex);
        Debug.Log($"Stage {CurrentStageIndex + 1} time over. Call RestartCurrentStage() from your failure UI.", this);
    }

    private void GetCurrentGoals(out LightGoalButton mainGoal, out LightGoalButton subGoal)
    {
        if (HasStageSequence)
        {
            StageDefinition stage = CurrentStage;
            mainGoal = stage != null ? stage.mainLightButton : null;
            subGoal = stage != null ? stage.subLightButton : null;
            return;
        }

        mainGoal = mainLightButton;
        subGoal = subLightButton;
    }

    private StageTimer GetCurrentTimer()
    {
        return HasStageSequence ? CurrentStage?.timer : timer;
    }

    private StageCeiling GetCurrentExitGate()
    {
        return HasStageSequence ? CurrentStage?.exitGate : ceiling;
    }

    private void UnsubscribeAllTimers()
    {
        if (HasStageSequence)
        {
            for (int i = 0; i < stages.Count; i++)
            {
                if (stages[i]?.timer != null)
                    stages[i].timer.OnTimeOver -= HandleCurrentStageTimeOver;
            }
            return;
        }

        if (timer != null)
            timer.OnTimeOver -= HandleCurrentStageTimeOver;
    }
}