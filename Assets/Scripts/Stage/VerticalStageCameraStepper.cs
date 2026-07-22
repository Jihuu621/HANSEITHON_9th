using System.Collections;
using UnityEngine;

/// <summary>Smoothly moves the active camera rig alongside the stage elevator.</summary>
[DisallowMultipleComponent]
public sealed class VerticalStageCameraStepper : MonoBehaviour
{
    [SerializeField] private StageManager stageManager;
    [SerializeField] private Transform cameraRig;
    [SerializeField, Min(0.1f)] private float stageHeight = 10f;
    [SerializeField, Min(0.01f)] private float moveDuration = 0.8f;

    private Vector3 initialPosition;
    private Coroutine moveRoutine;

    private void Awake()
    {
        if (stageManager == null)
            stageManager = GetComponent<StageManager>();
        if (cameraRig != null)
            initialPosition = cameraRig.position;
    }

    private void OnEnable()
    {
        if (stageManager == null)
            stageManager = GetComponent<StageManager>();
        if (stageManager != null)
            stageManager.StageTransitionStarted += HandleStageTransitionStarted;
    }

    private void OnDisable()
    {
        if (stageManager != null)
            stageManager.StageTransitionStarted -= HandleStageTransitionStarted;
    }

    private void HandleStageTransitionStarted(int completedStageIndex, float transitionDuration)
    {
        if (cameraRig == null || stageManager == null || !stageManager.HasNextStage)
            return;

        Vector3 target = initialPosition + Vector3.up * (stageHeight * (completedStageIndex + 1));
        if (moveRoutine != null)
            StopCoroutine(moveRoutine);
        moveRoutine = StartCoroutine(MoveCamera(target, transitionDuration));
    }

    private IEnumerator MoveCamera(Vector3 target, float transitionDuration)
    {
        Vector3 start = cameraRig.position;
        float elapsed = 0f;
        float activeDuration = Mathf.Max(0.01f, transitionDuration > 0f ? transitionDuration : moveDuration);
        while (elapsed < activeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / activeDuration));
            cameraRig.position = Vector3.LerpUnclamped(start, target, t);
            yield return null;
        }

        cameraRig.position = target;
        moveRoutine = null;
    }
}