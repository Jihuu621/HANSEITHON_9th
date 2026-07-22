using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A fixed auxiliary light source. It has no movement logic and continuously emits in the
/// Emission Origin's right direction until a non-trigger obstacle blocks it.
/// </summary>
[DisallowMultipleComponent]
public sealed class StaticSubLight : MonoBehaviour
{
    private const int RaycastCapacity = 16;
    private static readonly List<StaticSubLight> ActiveLights = new();

    [Header("References")]
    [SerializeField] private Transform emissionOrigin;
    [SerializeField] private Shader beamShader;

    [Header("Emission")]
    [SerializeField, Min(0.1f)] private float beamLength = 8f;
    [SerializeField, Min(0.01f)] private float beamWidth = 0.14f;
    [SerializeField] private Color lightColor = new(1f, 0.88f, 0.55f, 1f);
    [SerializeField] private LayerMask blockingLayers = ~0;
    [SerializeField] private int sortingOrder = 24;
    [SerializeField] private bool enabledOnStart = true;

    private readonly RaycastHit2D[] raycastHits = new RaycastHit2D[RaycastCapacity];
    private readonly List<ColorChangeLaserHit> colorChanges = new(8);
    private LineRenderer beamRenderer;
    private Material beamMaterial;
    private PolygonCollider2D lightArea;
    private Vector2 beamEnd;
    private Color currentOutputColor;

    public bool IsEmitting { get; private set; }
    public Color CurrentColor => currentOutputColor;
    public Vector2 Origin => emissionOrigin != null ? emissionOrigin.position : transform.position;
    public Vector2 End => beamEnd;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry()
    {
        ActiveLights.Clear();
    }

    private void Reset()
    {
        CacheReferences();
    }

    private void Awake()
    {
        CacheReferences();
        CreateRuntimeOutput();
        IsEmitting = enabledOnStart;
        UpdateOutput();
        SetOutputEnabled(IsEmitting);
    }

    private void OnEnable()
    {
        if (!ActiveLights.Contains(this))
            ActiveLights.Add(this);
    }

    private void OnDisable()
    {
        ActiveLights.Remove(this);
        SetOutputEnabled(false);
    }

    private void LateUpdate()
    {
        if (!IsEmitting)
            return;

        UpdateOutput();
    }

    private void OnDestroy()
    {
        ActiveLights.Remove(this);
        if (beamMaterial != null)
            Destroy(beamMaterial);
    }

    public void SetEmitting(bool emitting)
    {
        IsEmitting = emitting;
        if (IsEmitting)
            UpdateOutput();
        SetOutputEnabled(IsEmitting);
    }

    public bool IsPointLit(Vector2 worldPoint)
    {
        return IsEmitting && lightArea != null && lightArea.enabled && lightArea.OverlapPoint(worldPoint);
    }

    public bool IsColliderLit(Collider2D target)
    {
        if (!IsEmitting || lightArea == null || target == null || !lightArea.enabled || !target.enabled)
            return false;

        ColliderDistance2D distance = Physics2D.Distance(lightArea, target);
        return distance.isValid && distance.isOverlapped;
    }

    /// <summary>Returns this beam's actual color at a position, before or after any laser crossings.</summary>
    public Color GetColorAtPoint(Vector2 worldPoint)
    {
        Vector2 beam = beamEnd - Origin;
        float beamT = beam.sqrMagnitude < 0.000001f ? 0f : Vector2.Dot(worldPoint - Origin, beam) / beam.sqrMagnitude;
        return ColorChangeLaser.EvaluateColorAt(lightColor, colorChanges, beamT);
    }

    public static bool TryGetLightTouching(Collider2D target, out Color color)
    {
        color = Color.white;
        if (target == null)
            return false;

        for (int i = ActiveLights.Count - 1; i >= 0; i--)
        {
            StaticSubLight light = ActiveLights[i];
            if (light == null)
            {
                ActiveLights.RemoveAt(i);
                continue;
            }

            if (!light.isActiveAndEnabled || !light.IsColliderLit(target))
                continue;

            color = light.GetColorAtPoint(target.bounds.center);
            return true;
        }

        return false;
    }

    private void UpdateOutput()
    {
        if (emissionOrigin == null)
            return;

        Vector2 origin = emissionOrigin.position;
        Vector2 direction = emissionOrigin.right.normalized;
        beamEnd = GetBeamEnd(origin, direction);

        if (beamRenderer != null)
        {
            beamRenderer.startWidth = beamWidth;
            beamRenderer.endWidth = beamWidth;
            beamRenderer.SetPosition(0, origin);
            beamRenderer.SetPosition(1, beamEnd);
            ColorChangeLaser.GetColorChangesAlongBeam(origin, beamEnd, colorChanges);
            currentOutputColor = ColorChangeLaser.ApplyChangesToLineRenderer(beamRenderer, lightColor, colorChanges);
        }

        if (lightArea == null)
            return;

        float halfWidth = beamWidth * 0.5f;
        Vector2 side = Vector2.Perpendicular(direction) * halfWidth;
        Vector2 startLocal = transform.InverseTransformPoint(origin);
        Vector2 endLocal = transform.InverseTransformPoint(beamEnd);
        Vector2 sideLocal = transform.InverseTransformVector(side);
        lightArea.pathCount = 1;
        lightArea.SetPath(0, new[]
        {
            startLocal - sideLocal,
            startLocal + sideLocal,
            endLocal + sideLocal,
            endLocal - sideLocal
        });
    }

    private Vector2 GetBeamEnd(Vector2 origin, Vector2 direction)
    {
        ContactFilter2D filter = new();
        filter.SetLayerMask(blockingLayers);
        filter.useTriggers = false;

        int hitCount = Physics2D.Raycast(origin, direction, filter, raycastHits, beamLength);
        float nearestDistance = beamLength;
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = raycastHits[i].collider;
            if (hit == null || MainLightController.IsPlayerLightPassThrough(hit) || hit.transform == transform || hit.transform.IsChildOf(transform))
                continue;

            nearestDistance = Mathf.Min(nearestDistance, raycastHits[i].distance);
        }

        return origin + direction * nearestDistance;
    }

    private void CreateRuntimeOutput()
    {
        Shader selectedShader = beamShader != null ? beamShader : Shader.Find("HANSEITHON/PrismLaser");
        if (selectedShader == null)
        {
            Debug.LogError("StaticSubLight requires the PrismLaser shader.", this);
            return;
        }

        GameObject beamObject = new("Static Sub Light Beam");
        beamObject.transform.SetParent(transform, false);
        beamRenderer = beamObject.AddComponent<LineRenderer>();
        beamRenderer.useWorldSpace = true;
        beamRenderer.positionCount = 2;
        beamRenderer.numCapVertices = 3;
        beamRenderer.numCornerVertices = 2;
        beamRenderer.alignment = LineAlignment.View;
        beamRenderer.textureMode = LineTextureMode.Stretch;
        beamRenderer.sortingOrder = sortingOrder;
        beamMaterial = new Material(selectedShader) { name = "Static Sub Light Beam Material (Runtime)" };
        beamMaterial.SetColor("_Color", Color.white);
        beamRenderer.sharedMaterial = beamMaterial;

        GameObject areaObject = new("Static Sub Light Area");
        areaObject.transform.SetParent(transform, false);
        lightArea = areaObject.AddComponent<PolygonCollider2D>();
        lightArea.isTrigger = true;
    }

    private void SetOutputEnabled(bool enabled)
    {
        if (beamRenderer != null)
            beamRenderer.enabled = enabled;
        if (lightArea != null)
            lightArea.enabled = enabled;
    }

    private void CacheReferences()
    {
        if (emissionOrigin == null)
            emissionOrigin = transform.Find("Emission Origin");
    }

    private void OnValidate()
    {
        beamLength = Mathf.Max(0.1f, beamLength);
        beamWidth = Mathf.Max(0.01f, beamWidth);
        CacheReferences();
    }

    private void OnDrawGizmosSelected()
    {
        CacheReferences();
        if (emissionOrigin == null)
            return;

        Gizmos.color = lightColor;
        Gizmos.DrawLine(emissionOrigin.position, emissionOrigin.position + emissionOrigin.right * beamLength);
        Gizmos.DrawWireSphere(emissionOrigin.position, beamWidth * 0.5f);
    }
}