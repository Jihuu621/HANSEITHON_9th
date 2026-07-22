using System.Collections.Generic;
using UnityEngine;

public readonly struct ColorChangeLaserHit
{
    public ColorChangeLaserHit(float beamT, Color color)
    {
        BeamT = beamT;
        Color = color;
    }

    public float BeamT { get; }
    public Color Color { get; }
}

/// <summary>
/// A non-light laser that recolors the main beam after the beam crosses it.
/// The laser itself never activates light-sensitive puzzles.
/// </summary>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public sealed class ColorChangeLaser : MonoBehaviour
{
    private const int RaycastCapacity = 16;
    private static readonly List<ColorChangeLaser> ActiveLasers = new();

    [Header("References")]
    [SerializeField] private Transform laserOrigin;
    [SerializeField] private Shader laserShader;

    [Header("Laser")]
    [SerializeField] private bool startsEnabled = true;
    [SerializeField, Min(0.1f)] private float laserLength = 10f;
    [SerializeField, Min(0.005f)] private float laserWidth = 0.055f;
    [SerializeField] private Color laserColor = Color.red;
    [SerializeField] private LayerMask blockingLayers = ~0;
    [SerializeField] private int sortingOrder = 30;

    private readonly RaycastHit2D[] raycastHits = new RaycastHit2D[RaycastCapacity];
    private LineRenderer lineRenderer;
    private Material lineMaterial;
    private Vector2 laserEnd;

    public bool IsActiveLaser { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry()
    {
        ActiveLasers.Clear();
    }

    private void Reset()
    {
        CacheReferences();
    }

    private void Awake()
    {
        CacheReferences();
        CreateRuntimeVisual();
        IsActiveLaser = startsEnabled;
        UpdateLaser();
        SetRendererEnabled(IsActiveLaser);
    }

    private void OnEnable()
    {
        if (!ActiveLasers.Contains(this))
            ActiveLasers.Add(this);
    }

    private void OnDisable()
    {
        ActiveLasers.Remove(this);
        IsActiveLaser = false;
        SetRendererEnabled(false);
    }

    private void LateUpdate()
    {
        if (!IsActiveLaser)
            return;

        UpdateLaser();
    }

    private void OnDestroy()
    {
        ActiveLasers.Remove(this);
        if (lineMaterial != null)
            Destroy(lineMaterial);
    }

    public void SetLaserActive(bool active)
    {
        IsActiveLaser = active;
        if (IsActiveLaser)
            UpdateLaser();
        SetRendererEnabled(IsActiveLaser);
    }

    public static void AppendActiveLaserSegments(List<PrismLaserSegment> output)
    {
        if (output == null)
            return;

        for (int i = ActiveLasers.Count - 1; i >= 0; i--)
        {
            ColorChangeLaser laser = ActiveLasers[i];
            if (laser == null)
            {
                ActiveLasers.RemoveAt(i);
                continue;
            }

            if (!laser.isActiveAndEnabled || !laser.IsActiveLaser || laser.laserOrigin == null)
                continue;

            output.Add(new PrismLaserSegment(
                laser.laserOrigin.position,
                laser.laserEnd,
                laser.laserColor,
                laser.laserWidth * 0.5f));
        }
    }

    public static void GetColorChangesAlongBeam(Vector2 beamStart, Vector2 beamEnd, List<ColorChangeLaserHit> output)
    {
        if (output == null)
            return;

        output.Clear();
        if ((beamEnd - beamStart).sqrMagnitude < 0.000001f)
            return;

        // MainLightController reads both sources. Auxiliary lights must use the identical source list.
        var segments = new List<PrismLaserSegment>(16);
        PrismController.AppendEmittingLaserSegments(segments);
        AppendActiveLaserSegments(segments);

        for (int i = 0; i < segments.Count; i++)
        {
            PrismLaserSegment segment = segments[i];
            if (TryGetSegmentIntersection(beamStart, beamEnd, segment.Start, segment.End, out float beamT))
                output.Add(new ColorChangeLaserHit(beamT, segment.Color));
        }

        output.Sort(CompareHits);
    }

    public static Color EvaluateColorAt(Color baseColor, List<ColorChangeLaserHit> changes, float beamT)
    {
        if (changes == null || changes.Count == 0)
            return baseColor;

        Color color = baseColor;
        bool hasConverted = false;
        float clampedT = Mathf.Clamp01(beamT);
        for (int i = 0; i < changes.Count && changes[i].BeamT <= clampedT; i++)
        {
            color = hasConverted ? BlendColors(color, changes[i].Color) : changes[i].Color;
            hasConverted = true;
        }

        return color;
    }

    public static Color BlendColors(Color current, Color incoming)
    {
        return new Color((current.r + incoming.r) * 0.5f, (current.g + incoming.g) * 0.5f, (current.b + incoming.b) * 0.5f, 1f);
    }

    public static Color ApplyChangesToLineRenderer(LineRenderer line, Color baseColor, List<ColorChangeLaserHit> changes)
    {
        if (line == null)
            return baseColor;

        Color activeColor = baseColor;
        bool hasConverted = false;
        var colorKeys = new List<GradientColorKey> { new(baseColor, 0f) };
        if (changes != null)
        {
            for (int i = 0; i < changes.Count; i++)
            {
                float hitT = Mathf.Clamp(changes[i].BeamT, 0.001f, 0.999f);
                colorKeys.Add(new GradientColorKey(activeColor, hitT - 0.001f));
                activeColor = hasConverted ? BlendColors(activeColor, changes[i].Color) : changes[i].Color;
                hasConverted = true;
                colorKeys.Add(new GradientColorKey(activeColor, hitT + 0.001f));
            }
        }

        colorKeys.Add(new GradientColorKey(activeColor, 1f));
        Gradient gradient = new();
        gradient.SetKeys(colorKeys.ToArray(), new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        line.colorGradient = gradient;
        return activeColor;
    }

    private static int CompareHits(ColorChangeLaserHit left, ColorChangeLaserHit right)
    {
        return left.BeamT.CompareTo(right.BeamT);
    }

    private static bool TryGetSegmentIntersection(Vector2 beamStart, Vector2 beamEnd, Vector2 laserStart, Vector2 laserEnd, out float beamT)
    {
        Vector2 beam = beamEnd - beamStart;
        Vector2 laser = laserEnd - laserStart;
        float denominator = Cross(beam, laser);
        if (Mathf.Abs(denominator) < 0.00001f)
        {
            beamT = 0f;
            return false;
        }

        Vector2 offset = laserStart - beamStart;
        float t = Cross(offset, laser) / denominator;
        float u = Cross(offset, beam) / denominator;
        beamT = t;
        return t >= 0f && t <= 1f && u >= 0f && u <= 1f;
    }

    private static float Cross(Vector2 left, Vector2 right)
    {
        return left.x * right.y - left.y * right.x;
    }
    private void UpdateLaser()
    {
        if (laserOrigin == null)
            return;

        Vector2 origin = laserOrigin.position;
        Vector2 direction = laserOrigin.right.normalized;
        laserEnd = GetLaserEnd(origin, direction);

        if (lineRenderer == null)
            return;

        lineRenderer.startWidth = laserWidth;
        lineRenderer.endWidth = laserWidth;
        lineRenderer.startColor = laserColor;
        lineRenderer.endColor = laserColor;
        lineRenderer.SetPosition(0, origin);
        lineRenderer.SetPosition(1, laserEnd);
    }

    private Vector2 GetLaserEnd(Vector2 origin, Vector2 direction)
    {
        ContactFilter2D filter = new();
        filter.SetLayerMask(blockingLayers);
        filter.useTriggers = false;

        int hitCount = Physics2D.Raycast(origin, direction, filter, raycastHits, laserLength);
        float nearestDistance = laserLength;
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = raycastHits[i].collider;
            if (hit == null || MainLightController.IsPlayerLightPassThrough(hit) || hit.transform == transform || hit.transform.IsChildOf(transform))
                continue;

            nearestDistance = Mathf.Min(nearestDistance, raycastHits[i].distance);
        }

        return origin + direction * nearestDistance;
    }

    private void CreateRuntimeVisual()
    {
        Shader selectedShader = laserShader != null ? laserShader : Shader.Find("HANSEITHON/PrismLaser");
        if (selectedShader == null)
        {
            Debug.LogError("ColorChangeLaser requires the PrismLaser shader.", this);
            return;
        }

        GameObject visual = new("Color Change Laser Visual");
        visual.transform.SetParent(transform, false);
        lineRenderer = visual.AddComponent<LineRenderer>();
        lineRenderer.useWorldSpace = true;
        lineRenderer.positionCount = 2;
        lineRenderer.numCapVertices = 3;
        lineRenderer.numCornerVertices = 2;
        lineRenderer.alignment = LineAlignment.View;
        lineRenderer.textureMode = LineTextureMode.Stretch;
        lineRenderer.sortingOrder = sortingOrder;
        lineMaterial = new Material(selectedShader) { name = "Color Change Laser Material (Runtime)" };
        lineMaterial.SetColor("_Color", Color.white);
        lineRenderer.sharedMaterial = lineMaterial;
    }

    private void SetRendererEnabled(bool enabled)
    {
        if (lineRenderer != null)
            lineRenderer.enabled = enabled;
    }

    private void CacheReferences()
    {
        if (laserOrigin == null)
            laserOrigin = transform.Find("Laser Origin");
    }

    private void OnValidate()
    {
        laserLength = Mathf.Max(0.1f, laserLength);
        laserWidth = Mathf.Max(0.005f, laserWidth);
        CacheReferences();
    }

    private void OnDrawGizmosSelected()
    {
        CacheReferences();
        if (laserOrigin == null)
            return;

        Gizmos.color = laserColor;
        Gizmos.DrawLine(laserOrigin.position, laserOrigin.position + laserOrigin.right * laserLength);
    }
}