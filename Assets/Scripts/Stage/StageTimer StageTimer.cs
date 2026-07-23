using System;
using TMPro;
using UnityEngine;

/// <summary>Countdown display. Assign a TMP text already placed in the scene UI.</summary>
public class StageTimer : MonoBehaviour
{
    [Header("Time Limit")]
    [Min(1f)] public float limitTime = 25f;
    [Tooltip("Use the TextMeshPro text inside the timer frame you created.")]
    public TMP_Text timerText;
    public Action OnTimeOver;
    public event Action<StageTimer> TimeChanged;

    private float currentTime;
    private bool isRunning;

    public float RemainingNormalized => limitTime <= 0f ? 0f : currentTime / limitTime;

    public void SetLimitTime(float seconds)
    {
        limitTime = Mathf.Max(1f, seconds);
    }

    private void Update()
    {
        if (!isRunning)
            return;

        currentTime = Mathf.Max(0f, currentTime - Time.deltaTime);
        RefreshText();
        TimeChanged?.Invoke(this);
        if (currentTime > 0f)
            return;

        isRunning = false;
        OnTimeOver?.Invoke();
    }

    public void StartTimer()
    {
        currentTime = limitTime;
        isRunning = true;
        RefreshText();
        TimeChanged?.Invoke(this);
    }

    public void StopTimer()
    {
        isRunning = false;
    }

    private void RefreshText()
    {
        if (timerText != null)
            timerText.text = $"{Mathf.CeilToInt(currentTime)}\uCD08";
    }
}