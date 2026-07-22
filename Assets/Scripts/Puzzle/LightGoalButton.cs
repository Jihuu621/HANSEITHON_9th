using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A non-blocking light goal. It is pressed only while its explicitly assigned light source overlaps it.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class LightGoalButton : MonoBehaviour
{
    public enum LightSourceRole
    {
        Assigned,
        MovingMainLight,
        FixedSubLight
    }
    [Header("Required Light")]
    [SerializeField] private LightSourceRole sourceRole = LightSourceRole.Assigned;
    [SerializeField] private MainLightController requiredLight;
    [SerializeField] private StageManager stageManager;

    [Header("Color Requirement")]
    [SerializeField] private bool requireColor;
    [SerializeField] private Color requiredColor = Color.red;
    [SerializeField, Range(0.01f, 1f)] private float colorTolerance = 0.18f;

    [Header("References")]
    [SerializeField] private BoxCollider2D sensor;
    [SerializeField] private SpriteRenderer visual;

    [Header("Feedback")]
    [SerializeField] private Color idleColor = new(0.2f, 0.25f, 0.3f, 1f);
    [SerializeField] private Color pressedColor = new(1f, 0.82f, 0.3f, 1f);

    private readonly List<PrismLaserSegment> prismLaserSegments = new(3);
    private readonly RaycastHit2D[] prismHitBuffer = new RaycastHit2D[16];

    public bool IsPressed { get; private set; }
    public MainLightController RequiredLight => requiredLight;

    private void Reset()
    {
        CacheReferences();
        ConfigureSensor();
    }

    private void Awake()
    {
        CacheReferences();
        ConfigureSensor();
        SetVisualState(false);
    }

    private void Update()
    {
        if (stageManager == null)
            stageManager = FindAnyObjectByType<StageManager>();

        ResolveRequiredLight();
        bool pressed = IsRequiredLightOverlappingSensor();
        if (pressed == IsPressed)
            return;

        IsPressed = pressed;
        SetVisualState(IsPressed);
        if (stageManager != null)
            stageManager.NotifyGoalButtonChanged(this);
    }

    private bool IsRequiredLightOverlappingSensor()
    {
        if (sensor == null)
            return false;

        // Prism RGB lasers are direct valid sources for color buttons.
        if (requireColor && IsPrismLaserOverlappingSensor())
            return true;

        if (requiredLight == null)
            return false;

        Vector2 samplePoint = sensor.bounds.center;
        if (requiredLight.IsColliderLit(sensor))
            return IsColorAccepted(requiredLight.GetColorAtPoint(samplePoint));

        // A triggered emitter is a reflected continuation of its input light.
        LightTriggeredEmitter[] emitters = FindObjectsByType<LightTriggeredEmitter>(FindObjectsInactive.Exclude);
        for (int i = 0; i < emitters.Length; i++)
        {
            LightTriggeredEmitter emitter = emitters[i];
            if (emitter != null && emitter.InputLight == requiredLight && emitter.IsColliderLit(sensor))
                return IsColorAccepted(emitter.GetColorAtPoint(samplePoint));
        }

        return false;
    }

    private bool IsPrismLaserOverlappingSensor()
    {
        prismLaserSegments.Clear();
        PrismController.AppendEmittingLaserSegments(prismLaserSegments);

        for (int i = 0; i < prismLaserSegments.Count; i++)
        {
            PrismLaserSegment segment = prismLaserSegments[i];
            if (IsColorAccepted(segment.Color) && IsLaserSegmentTouchingSensor(segment))
                return true;
        }

        return false;
    }

    private bool IsLaserSegmentTouchingSensor(PrismLaserSegment segment)
    {
        Vector2 laserVector = segment.End - segment.Start;
        float distance = laserVector.magnitude;
        if (distance < 0.0001f)
            return sensor.OverlapPoint(segment.Start);

        ContactFilter2D filter = new();
        filter.SetLayerMask(~0);
        filter.useTriggers = true;
        int hitCount = Physics2D.Raycast(segment.Start, laserVector / distance, filter, prismHitBuffer, distance);
        for (int i = 0; i < hitCount; i++)
        {
            if (prismHitBuffer[i].collider == sensor)
                return true;
        }

        return sensor.OverlapPoint(segment.Start) || sensor.OverlapPoint(segment.End);
    }

    private bool IsColorAccepted(Color incomingColor)
    {
        if (!requireColor)
            return true;

        Vector3 difference = new(
            incomingColor.r - requiredColor.r,
            incomingColor.g - requiredColor.g,
            incomingColor.b - requiredColor.b);
        return difference.sqrMagnitude <= colorTolerance * colorTolerance;
    }

    private void ResolveRequiredLight()
    {
        if (sourceRole == LightSourceRole.Assigned || requiredLight != null)
            return;

        MainLightController[] lights = FindObjectsByType<MainLightController>(FindObjectsInactive.Exclude);
        for (int i = 0; i < lights.Length; i++)
        {
            bool isFixed = lights[i].IsVerticallyFixed;
            if ((sourceRole == LightSourceRole.MovingMainLight && !isFixed)
                || (sourceRole == LightSourceRole.FixedSubLight && isFixed))
            {
                requiredLight = lights[i];
                return;
            }
        }
    }
    private void CacheReferences()
    {
        if (sensor == null)
            sensor = GetComponent<BoxCollider2D>();
        if (visual == null)
            visual = GetComponentInChildren<SpriteRenderer>(true);
    }

    private void ConfigureSensor()
    {
        if (sensor != null)
            sensor.isTrigger = true;
    }

    private void SetVisualState(bool pressed)
    {
        if (visual != null)
            visual.color = pressed ? pressedColor : idleColor;
    }

    private void OnValidate()
    {
        CacheReferences();
        ConfigureSensor();
    }
}