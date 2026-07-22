using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Controls vertical stage progression inside one scene.</summary>
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
    [SerializeField, Min(0.1f)] private float elevatorDuration = 2.4f;
    [SerializeField] private float nextStageFloorOffsetY = -4.35f;

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
    public bool IsTransitioning { get; private set; }

    public event Action<int> StageCompleted;
    public event Action<int, float> StageTransitionStarted;
    public event Action<int> StageFailed;

    private Coroutine transitionRoutine;
    private bool HasStageSequence => stages != null && stages.Count > 0;
    private StageDefinition CurrentStage => HasStageSequence ? stages[CurrentStageIndex] : null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
            Debug.LogWarning("Multiple StageManagers were found. The newest instance will be used.", this);
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

    public void NotifyGoalButtonChanged(LightGoalButton changedButton)
    {
        if (changedButton == null || IsClear || IsTransitioning)
            return;

        GetCurrentGoals(out LightGoalButton mainGoal, out LightGoalButton subGoal);
        if (changedButton != mainGoal && changedButton != subGoal)
            return;

        TryCompleteCurrentStage();
    }

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
        SetOnlyStageActive(stageIndex);
        CurrentStageIndex = stageIndex;
        IsClear = false;
        IsTransitioning = false;

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
        if (IsClear || IsTransitioning)
            return;

        GetCurrentGoals(out LightGoalButton mainGoal, out LightGoalButton subGoal);
        bool bothGoalsPressed = mainGoal != null && subGoal != null && mainGoal.IsPressed && subGoal.IsPressed;
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
        {
            if (transitionRoutine != null)
                StopCoroutine(transitionRoutine);
            transitionRoutine = StartCoroutine(TransitionToNextStage(CurrentStageIndex));
        }
        else if (HasStageSequence)
        {
            GameObject clearedStageRoot = GetStageRoot(CurrentStageIndex);
            SetStageLightInteractionEnabled(clearedStageRoot, false);
            if (clearedStageRoot != null)
                clearedStageRoot.SetActive(false);
        }
    }

    private IEnumerator TransitionToNextStage(int completedStageIndex)
    {
        int nextStageIndex = completedStageIndex + 1;
        GameObject clearedStageRoot = GetStageRoot(completedStageIndex);
        GameObject nextStageRoot = GetStageRoot(nextStageIndex);

        SetStageLightInteractionEnabled(clearedStageRoot, false);
        IsTransitioning = true;
        StageTransitionStarted?.Invoke(completedStageIndex, elevatorDuration);

        Transform floor = FindLowestSolidFloor(clearedStageRoot);
        if (floor != null)
        {
            // Detach the floor before disabling the cleared stage: it becomes the next stage's elevator floor.
            floor.SetParent(transform, true);
            StageElevator elevator = floor.GetComponent<StageElevator>();
            if (elevator == null)
                elevator = floor.gameObject.AddComponent<StageElevator>();

            Vector3 destination = floor.position;
            if (nextStageRoot != null)
                destination.y = nextStageRoot.transform.position.y + nextStageFloorOffsetY;
            yield return elevator.MoveTo(destination, elevatorDuration);
        }
        else
        {
            yield return new WaitForSeconds(elevatorDuration);
        }

        if (clearedStageRoot != null)
            clearedStageRoot.SetActive(false);

        transitionRoutine = null;
        BeginStage(nextStageIndex);
    }

    private void HandleCurrentStageTimeOver()
    {
        if (IsClear || IsTransitioning)
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

    private StageTimer GetCurrentTimer() => HasStageSequence ? CurrentStage?.timer : timer;
    private StageCeiling GetCurrentExitGate() => HasStageSequence ? CurrentStage?.exitGate : ceiling;

    private void SetOnlyStageActive(int activeStageIndex)
    {
        if (!HasStageSequence)
            return;

        for (int i = 0; i < stages.Count; i++)
        {
            GameObject stageRoot = GetStageRoot(i);
            if (stageRoot != null)
                stageRoot.SetActive(i == activeStageIndex);
        }
    }

    private GameObject GetStageRoot(int stageIndex)
    {
        if (!HasStageSequence || stageIndex < 0 || stageIndex >= stages.Count)
            return null;

        LightGoalButton goal = stages[stageIndex]?.mainLightButton;
        return goal != null ? goal.transform.root.gameObject : null;
    }

    private static Transform FindLowestSolidFloor(GameObject stageRoot)
    {
        if (stageRoot == null)
            return null;

        Collider2D[] colliders = stageRoot.GetComponentsInChildren<Collider2D>(true);
        Collider2D lowest = null;
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D candidate = colliders[i];
            if (candidate == null || candidate.isTrigger)
                continue;

            if (lowest == null || candidate.bounds.center.y < lowest.bounds.center.y)
                lowest = candidate;
        }

        return lowest != null ? lowest.transform : null;
    }

    private static void SetStageLightInteractionEnabled(GameObject stageRoot, bool enabled)
    {
        if (stageRoot == null)
            return;

        AICompanionLightOperator[] operators = stageRoot.GetComponentsInChildren<AICompanionLightOperator>(true);
        for (int i = 0; i < operators.Length; i++)
            operators[i].enabled = enabled;

        if (enabled)
            return;

        MainLightController[] lights = stageRoot.GetComponentsInChildren<MainLightController>(true);
        for (int i = 0; i < lights.Length; i++)
            lights[i].SetBeamEnabled(false);
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