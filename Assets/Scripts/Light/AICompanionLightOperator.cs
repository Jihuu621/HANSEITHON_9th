using System;
using UnityEngine;

/// <summary>Moves the main light vertically through indirect AI commands.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MainLightController))]
public sealed class AICompanionLightOperator : MonoBehaviour
{
    [SerializeField] private MainLightController mainLight;

    [Header("AI Movement Commands")]
    [SerializeField] private KeyCode moveUpKey = KeyCode.UpArrow;
    [SerializeField] private KeyCode moveDownKey = KeyCode.DownArrow;
    [SerializeField] private KeyCode stopKey = KeyCode.Space;
    [SerializeField, Range(0.05f, 1f)] private float verticalSpeedNormalized = 0.22f;

    public bool IsMoving { get; private set; }
    public int MoveDirection { get; private set; }
    public event Action<string> CommandStateChanged;

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

        if (Input.GetKeyDown(moveUpKey))
            StartMoving(1);
        else if (Input.GetKeyDown(moveDownKey))
            StartMoving(-1);

        if (Input.GetKeyDown(stopKey))
            StopMoving();

        if (!IsMoving)
            return;

        float current = mainLight.VerticalNormalized;
        float next = Mathf.Clamp01(current + MoveDirection * verticalSpeedNormalized * Time.deltaTime);
        mainLight.SetVerticalNormalized(next);

        if (Mathf.Approximately(current, next))
            StopMoving();
    }

    public void StartMoving(int direction)
    {
        MoveDirection = direction >= 0 ? 1 : -1;
        IsMoving = true;
        CommandStateChanged?.Invoke(MoveDirection > 0 ? "MovingUp" : "MovingDown");
    }

    public void StopMoving()
    {
        if (!IsMoving)
            return;

        IsMoving = false;
        MoveDirection = 0;
        CommandStateChanged?.Invoke("MovementStopped");
    }

    private void OnDisable()
    {
        StopMoving();
    }
}
