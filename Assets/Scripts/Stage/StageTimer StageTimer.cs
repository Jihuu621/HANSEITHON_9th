using UnityEngine;
using TMPro;
using System;

public class StageTimer : MonoBehaviour
{
    [Header("제한 시간")]
    public float limitTime = 60f;

    private float currentTime;
    private bool isRunning = false;

    public TMP_Text timerText;
    public Action OnTimeOver;



    void Start()
    {
        StartTimer();
    }



    void Update()
    {
        if (!isRunning)
            return;

       
        currentTime -= Time.deltaTime;


        if (timerText != null)
        {
            timerText.text =
                Mathf.Ceil(currentTime).ToString();
        }


        if (currentTime <= 0)
        {
            currentTime = 0;
            isRunning = false;

            TimeOver();
        }
    }

   


    public void StartTimer()
    {
        currentTime = limitTime;
        isRunning = true;

        UpdateTimerUI();
    }



    public void StopTimer()
    {
        isRunning = false;
    }



    void TimeOver()
    {
        Debug.Log("시간 종료");

        OnTimeOver?.Invoke();
    }



    void UpdateTimerUI()
    {
        if (timerText != null)
        {
            timerText.text =
                Mathf.Ceil(currentTime).ToString();
        }
    }
}