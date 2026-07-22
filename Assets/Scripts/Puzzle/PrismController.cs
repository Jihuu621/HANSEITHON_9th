using System.Collections.Generic;
using UnityEngine;

public readonly struct PrismLaserSegment
{
    public PrismLaserSegment(Vector2 start, Vector2 end, Color color, float halfWidth)
    {
        Start = start;
        End = end;
        Color = color;
        HalfWidth = halfWidth;
    }

    public Vector2 Start { get; }
    public Vector2 End { get; }
    public Color Color { get; }
    public float HalfWidth { get; }
}

/// <summary>
/// 직접광을 받으면 한 꼭짓점에서 RGB 레이저 세 갈래를 방출한다.
/// 레이저는 빛 판정이 아니며, MainLightController의 색 변환 데이터로만 사용된다.
/// </summary>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(PolygonCollider2D))]
public sealed class PrismController : MonoBehaviour
{
    private const int LaserCount = 3;
    private const int RaycastCapacity = 16;
    private static readonly List<PrismController> ActivePrisms = new();

    [Header("References")]
    [SerializeField] private MainLightController lightSource;
    [SerializeField] private PolygonCollider2D prismCollider;
    [SerializeField] private Transform laserOrigin;
    [SerializeField] private Shader laserShader;

    [Header("Triangle")]
    [SerializeField, Min(0.1f)] private float triangleWidth = 1.5f;
    [SerializeField, Min(0.1f)] private float triangleHeight = 1.3f;
    [SerializeField] private Color unlitBodyColor = new(0.18f, 0.22f, 0.3f, 0.65f);
    [SerializeField] private Color litBodyColor = new(0.65f, 0.9f, 1f, 0.8f);

    [Header("RGB Lasers")]
    [SerializeField, Min(0.1f)] private float laserLength = 12f;
    [SerializeField, Min(0.005f)] private float laserWidth = 0.055f;
    [SerializeField] private float redAngle = 30f;
    [SerializeField] private float greenAngle = 0f;
    [SerializeField] private float blueAngle = -30f;
    [SerializeField] private Color redColor = new(1f, 0.04f, 0.02f, 1f);
    [SerializeField] private Color greenColor = new(0.05f, 1f, 0.15f, 1f);
    [SerializeField] private Color blueColor = new(0.05f, 0.25f, 1f, 1f);
    [SerializeField] private LayerMask laserBlockingLayers = ~0;
    [SerializeField] private int laserSortingOrder = 30;

    private readonly RaycastHit2D[] raycastHits = new RaycastHit2D[RaycastCapacity];
    private readonly Vector2[] laserEnds = new Vector2[LaserCount];
    private readonly LineRenderer[] laserRenderers = new LineRenderer[LaserCount];
    private readonly Material[] laserMaterials = new Material[LaserCount];

    private GameObject bodyVisualObject;
    private Mesh bodyMesh;
    private MeshRenderer bodyRenderer;
    private Material bodyMaterial;

    public bool IsEmitting { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry()
    {
        ActivePrisms.Clear();
    }

    private void Reset()
    {
        CacheReferences();
        ConfigureCollider();
    }

    private void Awake()
    {
        CacheReferences();
        ConfigureCollider();
        CreateRuntimeVisuals();
        UpdateLaserSegments(false);
    }

    private void OnEnable()
    {
        if (!ActivePrisms.Contains(this))
            ActivePrisms.Add(this);
    }

    private void OnDisable()
    {
        ActivePrisms.Remove(this);
        IsEmitting = false;
        SetLaserRenderersEnabled(false);
    }

    private void LateUpdate()
    {
        ResolveLightSource();
        IsEmitting = lightSource != null
            && prismCollider != null
            && lightSource.IsColliderReceivingBeam(prismCollider);

        UpdateLaserSegments(IsEmitting);
        UpdateBodyVisual();
    }

    private void OnDestroy()
    {
        ActivePrisms.Remove(this);

        if (bodyMesh != null)
            Destroy(bodyMesh);
        if (bodyMaterial != null)
            Destroy(bodyMaterial);

        for (int i = 0; i < laserMaterials.Length; i++)
        {
            if (laserMaterials[i] != null)
                Destroy(laserMaterials[i]);
        }
    }

    public static void AppendEmittingLaserSegments(List<PrismLaserSegment> output)
    {
        if (output == null)
            return;

        for (int prismIndex = ActivePrisms.Count - 1; prismIndex >= 0; prismIndex--)
        {
            PrismController prism = ActivePrisms[prismIndex];
            if (prism == null)
            {
                ActivePrisms.RemoveAt(prismIndex);
                continue;
            }

            if (!prism.isActiveAndEnabled || !prism.IsEmitting || prism.laserOrigin == null)
                continue;

            Vector2 start = prism.laserOrigin.position;
            Color[] colors = prism.GetLaserColors();
            for (int i = 0; i < LaserCount; i++)
            {
                output.Add(new PrismLaserSegment(
                    start,
                    prism.laserEnds[i],
                    colors[i],
                    prism.laserWidth * 0.5f));
            }
        }
    }

    private void UpdateLaserSegments(bool emitting)
    {
        if (laserOrigin == null)
            return;

        float[] angles = { redAngle, greenAngle, blueAngle };
        Vector2 origin = laserOrigin.position;
        Vector2 baseDirection = laserOrigin.right;
        ContactFilter2D filter = CreateLaserFilter();

        for (int i = 0; i < LaserCount; i++)
        {
            Vector2 direction = Rotate(baseDirection, angles[i]).normalized;
            laserEnds[i] = GetLaserEnd(origin, direction, filter);

            LineRenderer line = laserRenderers[i];
            if (line == null)
                continue;

            line.enabled = emitting;
            line.startWidth = laserWidth;
            line.endWidth = laserWidth;
            line.SetPosition(0, origin);
            line.SetPosition(1, laserEnds[i]);
        }
    }

    private Vector2 GetLaserEnd(Vector2 origin, Vector2 direction, ContactFilter2D filter)
    {
        int hitCount = Physics2D.Raycast(origin, direction, filter, raycastHits, laserLength);
        float nearestDistance = laserLength;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hitCollider = raycastHits[i].collider;
            if (hitCollider == null || hitCollider == prismCollider || MainLightController.IsPlayerLightPassThrough(hitCollider))
                continue;
            if (hitCollider.transform == transform || hitCollider.transform.IsChildOf(transform))
                continue;

            nearestDistance = Mathf.Min(nearestDistance, raycastHits[i].distance);
        }

        return origin + direction * nearestDistance;
    }

    private ContactFilter2D CreateLaserFilter()
    {
        var filter = new ContactFilter2D();
        filter.SetLayerMask(laserBlockingLayers);
        filter.useTriggers = false;
        return filter;
    }

    private void CreateRuntimeVisuals()
    {
        if (!Application.isPlaying)
            return;

        Shader selectedShader = laserShader != null
            ? laserShader
            : Shader.Find("HANSEITHON/PrismLaser");
        if (selectedShader == null)
        {
            Debug.LogError("PrismLaser shader를 찾을 수 없습니다.", this);
            return;
        }

        CreateBodyVisual(selectedShader);
        CreateLaserVisuals(selectedShader);
    }

    private void CreateBodyVisual(Shader selectedShader)
    {
        bodyVisualObject = new GameObject("Prism Body Visual");
        bodyVisualObject.transform.SetParent(transform, false);

        var meshFilter = bodyVisualObject.AddComponent<MeshFilter>();
        bodyRenderer = bodyVisualObject.AddComponent<MeshRenderer>();
        bodyRenderer.sortingOrder = laserSortingOrder - 1;

        bodyMesh = new Mesh { name = "Prism Triangle Mesh" };
        bodyMesh.vertices = GetTriangleVertices3D();
        bodyMesh.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 0.5f) };
        bodyMesh.colors = new[] { Color.white, Color.white, Color.white };
        bodyMesh.triangles = new[] { 0, 1, 2 };
        bodyMesh.RecalculateBounds();
        meshFilter.sharedMesh = bodyMesh;

        bodyMaterial = new Material(selectedShader) { name = "Prism Body Material (Runtime)" };
        bodyRenderer.sharedMaterial = bodyMaterial;
        UpdateBodyVisual();
    }

    private void CreateLaserVisuals(Shader selectedShader)
    {
        Color[] colors = GetLaserColors();
        string[] names = { "Red Laser", "Green Laser", "Blue Laser" };

        for (int i = 0; i < LaserCount; i++)
        {
            var laserObject = new GameObject(names[i]);
            laserObject.transform.SetParent(transform, false);

            LineRenderer line = laserObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.numCapVertices = 4;
            line.numCornerVertices = 2;
            line.sortingOrder = laserSortingOrder;
            line.startColor = colors[i];
            line.endColor = colors[i];

            Material material = new(selectedShader) { name = names[i] + " Material (Runtime)" };
            material.SetColor("_Color", Color.white);
            material.SetFloat("_Intensity", 3.5f);
            line.sharedMaterial = material;

            laserRenderers[i] = line;
            laserMaterials[i] = material;
        }
    }

    private void UpdateBodyVisual()
    {
        if (bodyMaterial == null)
            return;

        bodyMaterial.SetColor("_Color", IsEmitting ? litBodyColor : unlitBodyColor);
        bodyMaterial.SetFloat("_Intensity", IsEmitting ? 1.8f : 0.65f);
    }

    private void SetLaserRenderersEnabled(bool enabled)
    {
        for (int i = 0; i < laserRenderers.Length; i++)
        {
            if (laserRenderers[i] != null)
                laserRenderers[i].enabled = enabled;
        }
    }

    private Color[] GetLaserColors()
    {
        return new[] { redColor, greenColor, blueColor };
    }

    private Vector3[] GetTriangleVertices3D()
    {
        float halfWidth = triangleWidth * 0.5f;
        float halfHeight = triangleHeight * 0.5f;
        return new[]
        {
            new Vector3(-halfWidth, -halfHeight, 0f),
            new Vector3(-halfWidth, halfHeight, 0f),
            new Vector3(halfWidth, 0f, 0f)
        };
    }

    private Vector2[] GetTriangleVertices2D()
    {
        Vector3[] vertices = GetTriangleVertices3D();
        return new[] { (Vector2)vertices[0], (Vector2)vertices[1], (Vector2)vertices[2] };
    }

    private void CacheReferences()
    {
        if (prismCollider == null)
            prismCollider = GetComponent<PolygonCollider2D>();
        if (laserOrigin == null)
            laserOrigin = transform.Find("Laser Origin");
    }

    private void ResolveLightSource()
    {
        if (lightSource == null)
            lightSource = FindAnyObjectByType<MainLightController>();
    }

    private void ConfigureCollider()
    {
        if (prismCollider != null)
        {
            prismCollider.isTrigger = false;
            prismCollider.pathCount = 1;
            prismCollider.SetPath(0, GetTriangleVertices2D());
        }

        if (laserOrigin != null)
            laserOrigin.localPosition = new Vector3(triangleWidth * 0.5f, 0f, 0f);
    }

    private static Vector2 Rotate(Vector2 direction, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);
        return new Vector2(
            direction.x * cos - direction.y * sin,
            direction.x * sin + direction.y * cos);
    }

    private void OnValidate()
    {
        triangleWidth = Mathf.Max(0.1f, triangleWidth);
        triangleHeight = Mathf.Max(0.1f, triangleHeight);
        laserLength = Mathf.Max(0.1f, laserLength);
        laserWidth = Mathf.Max(0.005f, laserWidth);
        CacheReferences();
        ConfigureCollider();
    }

    private void OnDrawGizmosSelected()
    {
        Vector3[] vertices = GetTriangleVertices3D();
        Gizmos.color = new Color(0.5f, 0.9f, 1f, 0.9f);
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 from = transform.TransformPoint(vertices[i]);
            Vector3 to = transform.TransformPoint(vertices[(i + 1) % vertices.Length]);
            Gizmos.DrawLine(from, to);
        }

        Vector3 origin = laserOrigin != null
            ? laserOrigin.position
            : transform.TransformPoint(new Vector3(triangleWidth * 0.5f, 0f, 0f));
        float[] angles = { redAngle, greenAngle, blueAngle };
        Color[] colors = GetLaserColors();
        Vector2 baseDirection = laserOrigin != null ? laserOrigin.right : transform.right;
        for (int i = 0; i < LaserCount; i++)
        {
            Gizmos.color = colors[i];
            Gizmos.DrawLine(origin, origin + (Vector3)(Rotate(baseDirection, angles[i]).normalized * laserLength));
        }
    }
}
