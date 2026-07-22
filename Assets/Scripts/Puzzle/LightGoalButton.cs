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

    [Header("References")]
    [SerializeField] private BoxCollider2D sensor;
    [SerializeField] private SpriteRenderer visual;

    [Header("Feedback")]
    [SerializeField] private Color idleColor = new(0.2f, 0.25f, 0.3f, 1f);
    [SerializeField] private Color pressedColor = new(1f, 0.82f, 0.3f, 1f);

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
        bool pressed = requiredLight != null && sensor != null && requiredLight.IsColliderLit(sensor);
        if (pressed == IsPressed)
            return;

        IsPressed = pressed;
        SetVisualState(IsPressed);
        if (stageManager != null)
            stageManager.NotifyGoalButtonChanged(this);
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