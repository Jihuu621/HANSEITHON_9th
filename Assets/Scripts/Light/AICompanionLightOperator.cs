using System;
using UnityEngine;

/// <summary>
/// 플레이어가 AI 친구에게 내리는 간접 명령을 메인 라이트 자동 동작으로 변환한다.
/// 1/넘패드1은 폭 루프, 2/넘패드2는 상하 이동 루프의 시작과 정지에 사용한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MainLightController))]
public sealed class AICompanionLightOperator : MonoBehaviour
{
    [SerializeField] private MainLightController mainLight;

    [Header("AI Command Keys")]
    [SerializeField] private KeyCode widthCommandKey = KeyCode.Alpha1;
    [SerializeField] private KeyCode widthCommandKeypad = KeyCode.Keypad1;
    [SerializeField] private KeyCode verticalCommandKey = KeyCode.Alpha2;
    [SerializeField] private KeyCode verticalCommandKeypad = KeyCode.Keypad2;
    [SerializeField] private KeyCode colorCommandKey = KeyCode.Alpha3;
    [SerializeField] private KeyCode colorCommandKeypad = KeyCode.Keypad3;

    [Header("AI Automation")]
    [SerializeField, Min(0.05f)] private float widthLoopSpeed = 0.55f;
    [SerializeField, Min(0.05f)] private float verticalLoopSpeed = 0.4f;

    public bool IsWidthLoopActive { get; private set; }
    public bool IsVerticalLoopActive { get; private set; }
    public event Action<string> CommandStateChanged;

    private float widthLoopTime;
    private float verticalLoopTime;

    private void Reset()
    {
        mainLight = GetComponent<MainLightController>();
    }

    private void Awake()
    {
        if (mainLight == null)
            mainLight = GetComponent<MainLightController>();
    }

    private void Update()
    {
        if (mainLight == null)
            return;

        if (Pressed(widthCommandKey, widthCommandKeypad))
            ToggleWidthLoop();

        if (Pressed(verticalCommandKey, verticalCommandKeypad))
            ToggleVerticalLoop();

        if (Pressed(colorCommandKey, colorCommandKeypad))
        {
            mainLight.ToggleColor();
            CommandStateChanged?.Invoke("ColorChanged");
        }

        if (IsWidthLoopActive)
        {
            widthLoopTime += Time.deltaTime * widthLoopSpeed;
            mainLight.SetSpreadNormalized(EvaluateSmoothLoop(widthLoopTime));
        }

        if (IsVerticalLoopActive)
        {
            verticalLoopTime += Time.deltaTime * verticalLoopSpeed;
            mainLight.SetVerticalNormalized(EvaluateSmoothLoop(verticalLoopTime));
        }
    }

    public void ToggleWidthLoop()
    {
        IsWidthLoopActive = !IsWidthLoopActive;
        if (IsWidthLoopActive)
        {
            widthLoopTime = PhaseFromValue(mainLight.SpreadNormalized);
            CommandStateChanged?.Invoke("WidthLoopStarted");
        }
        else
        {
            mainLight.FreezeEndWidth();
            CommandStateChanged?.Invoke("WidthLoopStopped");
        }
    }

    public void ToggleVerticalLoop()
    {
        IsVerticalLoopActive = !IsVerticalLoopActive;
        if (IsVerticalLoopActive)
        {
            verticalLoopTime = PhaseFromValue(mainLight.VerticalNormalized);
            CommandStateChanged?.Invoke("VerticalLoopStarted");
        }
        else
        {
            CommandStateChanged?.Invoke("VerticalLoopStopped");
        }
    }

    private static float EvaluateSmoothLoop(float phase)
    {
        return 0.5f - 0.5f * Mathf.Cos(phase * Mathf.PI);
    }

    private static float PhaseFromValue(float normalized)
    {
        return Mathf.Acos(1f - 2f * Mathf.Clamp01(normalized)) / Mathf.PI;
    }
    private static bool Pressed(KeyCode key, KeyCode keypadKey)
    {
        return Input.GetKeyDown(key) || Input.GetKeyDown(keypadKey);
    }
}
