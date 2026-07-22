using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Activates when the main light touches its trigger receiver, then emits a fixed-direction
/// auxiliary beam. The receiver is a trigger, so it never blocks the main light.
/// </summary>
[DefaultExecutionOrder(-90)]
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class LightTriggeredEmitter : MonoBehaviour
{
    private const int RaycastCapacity = 16;

    [Header("References")]
    [SerializeField] private MainLightController inputLight;
    [SerializeField] private BoxCollider2D receiver;
    [SerializeField] private Transform emissionOrigin;
    [SerializeField] private Shader beamShader;

    [Header("Emission")]
    [SerializeField, Min(0.1f)] private float beamLength = 8f;
    [SerializeField, Min(0.01f)] private float beamWidth = 0.14f;
    [SerializeField] private bool inheritInputColor = true;
    [SerializeField] private Color emissionColor = Color.cyan;
    [SerializeField] private LayerMask blockingLayers = ~0;
    [SerializeField] private int sortingOrder = 25;

    [Header("Output Light Area")]
    [SerializeField] private bool createOutputLightArea = true;

    private readonly RaycastHit2D[] raycastHits = new RaycastHit2D[RaycastCapacity];
    private readonly List<ColorChangeLaserHit> colorChanges = new(8);
    private LineRenderer beamRenderer;
    private Material beamMaterial;
    private PolygonCollider2D outputLightArea;
    private Vector2 beamEnd;
    private Color currentEmissionColor;

    public bool IsEmitting { get; private set; }
    public Color CurrentColor => currentEmissionColor;
    public Vector2 Origin => emissionOrigin != null ? emissionOrigin.position : transform.position;
    public Vector2 End => beamEnd;

    private void Reset()
    {
        CacheReferences();
        ConfigureReceiver();
    }

    private void Awake()
    {
        CacheReferences();
        ConfigureReceiver();
        CreateRuntimeOutput();
        SetOutputEnabled(false);
    }

    private void LateUpdate()
    {
        ResolveInputLight();
        IsEmitting = inputLight != null
            && receiver != null
            && inputLight.IsColliderReceivingBeam(receiver);

        if (!IsEmitting)
        {
            SetOutputEnabled(false);
            return;
        }

        currentEmissionColor = inheritInputColor
            ? inputLight.GetColorAtPoint(receiver.bounds.center)
            : emissionColor;
        UpdateOutput();
        SetOutputEnabled(true);
    }

    private void OnDestroy()
    {
        if (beamMaterial != null)
            Destroy(beamMaterial);
    }

    /// <summary>Returns whether this auxiliary beam currently covers a world point.</summary>
    public bool IsPointLit(Vector2 worldPoint)
    {
        return IsEmitting
            && outputLightArea != null
            && outputLightArea.enabled
            && outputLightArea.OverlapPoint(worldPoint);
    }

    /// <summary>Returns whether this auxiliary beam currently overlaps a collider.</summary>
    public bool IsColliderLit(Collider2D target)
    {
        if (!IsEmitting || outputLightArea == null || target == null || !outputLightArea.enabled || !target.enabled)
            return false;

        ColliderDistance2D distance = Physics2D.Distance(outputLightArea, target);
        return distance.isValid && distance.isOverlapped;
    }

    /// <summary>Returns the emitted light color at a world point, respecting laser crossings.</summary>
    public Color GetColorAtPoint(Vector2 worldPoint)
    {
        Vector2 beam = beamEnd - Origin;
        float beamT = beam.sqrMagnitude < 0.000001f ? 0f : Vector2.Dot(worldPoint - Origin, beam) / beam.sqrMagnitude;
        Color sourceColor = inheritInputColor && inputLight != null
            ? inputLight.GetColorAtPoint(receiver.bounds.center)
            : emissionColor;
        return ColorChangeLaser.EvaluateColorAt(sourceColor, colorChanges, beamT);
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
            currentEmissionColor = ColorChangeLaser.ApplyChangesToLineRenderer(beamRenderer, currentEmissionColor, colorChanges);
        }

        UpdateOutputLightArea(origin, direction);
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

    private void UpdateOutputLightArea(Vector2 origin, Vector2 direction)
    {
        if (outputLightArea == null)
            return;

        float halfWidth = beamWidth * 0.5f;
        Vector2 side = Vector2.Perpendicular(direction) * halfWidth;
        Vector2 startLocal = transform.InverseTransformPoint(origin);
        Vector2 endLocal = transform.InverseTransformPoint(beamEnd);
        Vector2 sideLocal = transform.InverseTransformVector(side);

        outputLightArea.pathCount = 1;
        outputLightArea.SetPath(0, new[]
        {
            startLocal - sideLocal,
            startLocal + sideLocal,
            endLocal + sideLocal,
            endLocal - sideLocal
        });
    }

    private void CreateRuntimeOutput()
    {
        Shader selectedShader = beamShader != null
            ? beamShader
            : Shader.Find("HANSEITHON/PrismLaser");
        if (selectedShader == null)
        {
            Debug.LogError("LightTriggeredEmitter requires the PrismLaser shader.", this);
            return;
        }

        GameObject beamObject = new("Emitted Light Beam");
        beamObject.transform.SetParent(transform, false);
        beamRenderer = beamObject.AddComponent<LineRenderer>();
        beamRenderer.useWorldSpace = true;
        beamRenderer.positionCount = 2;
        beamRenderer.numCapVertices = 3;
        beamRenderer.numCornerVertices = 2;
        beamRenderer.alignment = LineAlignment.View;
        beamRenderer.textureMode = LineTextureMode.Stretch;
        beamRenderer.sortingOrder = sortingOrder;
        beamMaterial = new Material(selectedShader) { name = "Triggered Emitter Beam Material (Runtime)" };
        beamMaterial.SetColor("_Color", Color.white);
        beamRenderer.sharedMaterial = beamMaterial;

        if (!createOutputLightArea)
            return;

        GameObject areaObject = new("Output Light Area");
        areaObject.transform.SetParent(transform, false);
        outputLightArea = areaObject.AddComponent<PolygonCollider2D>();
        outputLightArea.isTrigger = true;
    }

    private void SetOutputEnabled(bool enabled)
    {
        if (beamRenderer != null)
            beamRenderer.enabled = enabled;
        if (outputLightArea != null)
            outputLightArea.enabled = enabled;
    }

    private void CacheReferences()
    {
        if (receiver == null)
            receiver = GetComponent<BoxCollider2D>();
        if (emissionOrigin == null)
            emissionOrigin = transform.Find("Emission Origin");
    }

    private void ResolveInputLight()
    {
        if (inputLight == null)
            inputLight = FindAnyObjectByType<MainLightController>();
    }

    private void ConfigureReceiver()
    {
        if (receiver != null)
            receiver.isTrigger = false;
    }

    private void OnValidate()
    {
        beamLength = Mathf.Max(0.1f, beamLength);
        beamWidth = Mathf.Max(0.01f, beamWidth);
        CacheReferences();
        ConfigureReceiver();
    }

    private void OnDrawGizmosSelected()
    {
        CacheReferences();
        if (emissionOrigin == null)
            return;

        Gizmos.color = IsEmitting ? currentEmissionColor : new Color(0.2f, 0.8f, 1f, 0.8f);
        Gizmos.DrawLine(emissionOrigin.position, emissionOrigin.position + emissionOrigin.right * beamLength);
        Gizmos.DrawWireSphere(emissionOrigin.position, beamWidth * 0.5f);
    }
}