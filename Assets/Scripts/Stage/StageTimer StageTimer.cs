using System;
using TMPro;
using UnityEngine;

/// <summary>Countdown display using the StageTimerText object in GameUI.</summary>
public class StageTimer : MonoBehaviour
{
    [Header("Time Limit")]
    [Min(1f)] public float limitTime = 25f;
    [Tooltip("The TextMeshPro object inside the GameUI timer frame.")]
    public TMP_Text timerText;
    public Action OnTimeOver;
    public event Action<StageTimer> TimeChanged;

    private float currentTime;
    private bool isRunning;

    public float RemainingNormalized => limitTime <= 0f ? 0f : currentTime / limitTime;

    private void Awake()
    {
        ResolveTimerText();
    }

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
        ResolveTimerText();
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
        ResolveTimerText();
        if (timerText != null)
            timerText.text = $"{Mathf.CeilToInt(currentTime)}\uCD08";
    }

    private void ResolveTimerText()
    {
        if (timerText != null)
            return;

        GameObject timerObject = GameObject.Find("StageTimerText");
        if (timerObject != null)
            timerText = timerObject.GetComponent<TMP_Text>();
    }
}