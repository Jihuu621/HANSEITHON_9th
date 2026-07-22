using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 베젤에서 수평으로 발사되는 고정 방향 사다리꼴 메인 라이트.
/// 직접 입력을 받지 않으며 AI 오퍼레이터의 명령으로만 상태가 바뀐다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class MainLightController : MonoBehaviour
{
    private const float FixedBeamAngle = -90f;

    [Header("Prefab References")]
    [SerializeField] private Transform beamPivot;
    [SerializeField] private Light2D visualLight;
    [SerializeField] private PolygonCollider2D lightArea;

    [Header("Vertical Movement (local position)")]
    [SerializeField] private float minY = -4.25f;
    [SerializeField] private float maxY = 4.25f;

    [Header("Tapered Beam")]
    [SerializeField, Min(0.05f)] private float sourceWidth = 0.45f;
    [SerializeField, Min(0.1f)] private float beamLength = 14f;
    [SerializeField, Min(0.05f)] private float narrowEndWidth = 0.9f;
    [SerializeField, Min(0.05f)] private float wideEndWidth = 4f;
    [SerializeField, Min(0.01f)] private float widthTransitionTime = 0.25f;
    [SerializeField, Min(0f)] private float edgeSoftness = 0.2f;
    [SerializeField, Range(0f, 4f)] private float narrowBrightness = 1.2f;
    [SerializeField, Range(0f, 4f)] private float wideBrightness = 0.45f;
    [SerializeField] private bool startNarrow = true;
    [SerializeField] private LayerMask blockingLayers;

    [Header("Light Color")]
    [SerializeField] private Color primaryColor = new(1f, 0.88f, 0.55f, 1f);
    [SerializeField] private Color alternateColor = Color.cyan;

    private Color currentColor;
    private float currentEndWidth;
    private float targetEndWidth;
    private float widthVelocity;

    public Color CurrentColor => currentColor;
    public float Spread => currentEndWidth;
    public float SpreadNormalized => Mathf.InverseLerp(narrowEndWidth, wideEndWidth, currentEndWidth);
    public float Brightness { get; private set; }
    public float BeamLength => beamLength;
    public float VerticalNormalized => Mathf.InverseLerp(minY, maxY, transform.localPosition.y);
    public bool IsNarrow { get; private set; }
    public Vector2 Origin => beamPivot != null ? beamPivot.position : transform.position;
    public Vector2 Direction => beamPivot != null ? beamPivot.up : Vector2.right;

    public event Action<MainLightController> StateChanged;

    private void Reset()
    {
        CacheReferences();
        ConfigureRigidbody();
        ApplyFixedDirection();
    }

    private void Awake()
    {
        CacheReferences();
        ConfigureRigidbody();
        ApplyFixedDirection();

        currentColor = primaryColor;
        IsNarrow = startNarrow;
        targetEndWidth = IsNarrow ? narrowEndWidth : wideEndWidth;
        currentEndWidth = targetEndWidth;
        ApplyBeamGeometry();
    }

    private void Update()
    {
        UpdateWidthTransition();
    }

    public void SetVerticalNormalized(float normalized)
    {
        SetVerticalLocalY(Mathf.Lerp(minY, maxY, Mathf.Clamp01(normalized)));
    }

    public void SetVerticalLocalY(float localY)
    {
        Vector3 position = transform.localPosition;
        float clampedY = Mathf.Clamp(localY, minY, maxY);
        if (Mathf.Approximately(position.y, clampedY))
            return;

        position.y = clampedY;
        transform.localPosition = position;
        StateChanged?.Invoke(this);
    }

    public void SetNarrow(bool narrow)
    {
        SetEndWidth(narrow ? narrowEndWidth : wideEndWidth);
    }

    public void SetEndWidth(float width)
    {
        targetEndWidth = Mathf.Clamp(width, narrowEndWidth, wideEndWidth);
        IsNarrow = Mathf.Approximately(targetEndWidth, narrowEndWidth);
    }

    public void SetSpreadNormalized(float normalized)
    {
        SetEndWidth(Mathf.Lerp(narrowEndWidth, wideEndWidth, Mathf.Clamp01(normalized)));
    }

    /// <summary>AI의 자동 폭 조절을 현재 보이는 폭에서 즉시 멈춘다.</summary>
    public void FreezeEndWidth()
    {
        targetEndWidth = currentEndWidth;
        widthVelocity = 0f;
        IsNarrow = Mathf.Approximately(targetEndWidth, narrowEndWidth);
    }

    public void ToggleColor()
    {
        SetColor(currentColor == alternateColor ? primaryColor : alternateColor);
    }

    public void SetColor(Color color)
    {
        if (currentColor == color)
            return;

        currentColor = color;
        if (visualLight != null)
            visualLight.color = currentColor;

        StateChanged?.Invoke(this);
    }

    /// <summary>지점이 현재 사다리꼴 빔 안에 있고 차단물 뒤에 있지 않은지 검사한다.</summary>
    public bool IsPointLit(Vector2 worldPosition)
    {
        if (beamPivot == null)
            return false;

        Vector2 localPoint = beamPivot.InverseTransformPoint(worldPosition);
        if (localPoint.y < 0f || localPoint.y > beamLength)
            return false;

        float progress = Mathf.Clamp01(localPoint.y / beamLength);
        float allowedHalfWidth = Mathf.Lerp(sourceWidth, currentEndWidth, progress) * 0.5f;
        if (Mathf.Abs(localPoint.x) > allowedHalfWidth)
            return false;

        if (blockingLayers.value != 0)
        {
            RaycastHit2D hit = Physics2D.Linecast(Origin, worldPosition, blockingLayers);
            if (hit.collider != null)
                return false;
        }

        return true;
    }

    public bool Contains(Vector2 worldPosition) => IsPointLit(worldPosition);

    private void UpdateWidthTransition()
    {
        if (Mathf.Abs(currentEndWidth - targetEndWidth) < 0.001f)
        {
            currentEndWidth = targetEndWidth;
            widthVelocity = 0f;
            return;
        }

        currentEndWidth = Mathf.SmoothDamp(
            currentEndWidth,
            targetEndWidth,
            ref widthVelocity,
            widthTransitionTime,
            Mathf.Infinity,
            Time.deltaTime);

        ApplyBeamGeometry();
        StateChanged?.Invoke(this);
    }

    private void ApplyBeamGeometry()
    {
        float sourceHalfWidth = sourceWidth * 0.5f;
        float endHalfWidth = currentEndWidth * 0.5f;
        Vector2[] colliderPath =
        {
            new(-sourceHalfWidth, 0f),
            new(sourceHalfWidth, 0f),
            new(endHalfWidth, beamLength),
            new(-endHalfWidth, beamLength)
        };

        if (lightArea != null)
        {
            lightArea.pathCount = 1;
            lightArea.SetPath(0, colliderPath);
        }

        float widthRatio = Mathf.InverseLerp(narrowEndWidth, wideEndWidth, currentEndWidth);
        Brightness = Mathf.Lerp(narrowBrightness, wideBrightness, widthRatio);

        if (visualLight != null)
        {
            Vector3[] lightPath =
            {
                new(-sourceHalfWidth, 0f, 0f),
                new(sourceHalfWidth, 0f, 0f),
                new(endHalfWidth, beamLength, 0f),
                new(-endHalfWidth, beamLength, 0f)
            };

            visualLight.lightType = Light2D.LightType.Freeform;
            visualLight.SetShapePath(lightPath);
            visualLight.shapeLightFalloffSize = edgeSoftness;
            visualLight.intensity = Brightness;
            visualLight.color = currentColor;
        }
    }

    private void ApplyFixedDirection()
    {
        if (beamPivot != null)
            beamPivot.localRotation = Quaternion.Euler(0f, 0f, FixedBeamAngle);
    }

    private void CacheReferences()
    {
        if (beamPivot == null)
        {
            Transform found = transform.Find("Beam Pivot");
            beamPivot = found != null ? found : transform;
        }

        if (visualLight == null)
            visualLight = GetComponentInChildren<Light2D>(true);

        if (lightArea == null)
            lightArea = GetComponentInChildren<PolygonCollider2D>(true);
    }

    private void ConfigureRigidbody()
    {
        Rigidbody2D body = GetComponent<Rigidbody2D>();
        if (body == null)
            return;

        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.freezeRotation = true;
    }

    private void OnValidate()
    {
        if (maxY < minY) maxY = minY;
        wideEndWidth = Mathf.Max(wideEndWidth, narrowEndWidth);
        widthTransitionTime = Mathf.Max(0.01f, widthTransitionTime);

        CacheReferences();
        ApplyFixedDirection();

        currentColor = primaryColor;
        IsNarrow = startNarrow;
        targetEndWidth = IsNarrow ? narrowEndWidth : wideEndWidth;
        currentEndWidth = targetEndWidth;
        ApplyBeamGeometry();
    }
}
