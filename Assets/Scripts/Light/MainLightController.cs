using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 베젤에서 수평으로 발사되는 고정 방향 사다리꼴 메인 라이트.
/// Light2D Freeform 대신 직접 생성한 삼각형 메시로 안정적으로 렌더링한다.
/// 직접 입력을 받지 않으며 AI 오퍼레이터의 명령으로만 상태가 바뀐다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class MainLightController : MonoBehaviour
{
    private const float FixedBeamAngle = -90f;
    private const int RaycastHitCapacity = 16;
    private const int ColorProfileRows = 256;

    [Header("Prefab References")]
    [SerializeField] private Transform beamPivot;
    [SerializeField] private PolygonCollider2D lightArea;
    [SerializeField] private Shader beamShader;

    [Header("Vertical Movement (local position)")]
    [SerializeField] private float minY = -4.25f;
    [SerializeField] private float maxY = 4.25f;

    [Header("Tapered Beam")]
    [SerializeField, Min(0.05f)] private float sourceWidth = 0.45f;
    [SerializeField, Min(0f)] private float proceduralVisualOffset;
    [SerializeField, Min(0.1f)] private float beamLength = 14f;
    [SerializeField, Min(0.05f)] private float narrowEndWidth = 0.9f;
    [SerializeField, Min(0.05f)] private float wideEndWidth = 4f;
    [SerializeField, Min(0.01f)] private float widthTransitionTime = 0.25f;
    [SerializeField, Range(0f, 4f)] private float narrowBrightness = 1.2f;
    [SerializeField, Range(0f, 4f)] private float wideBrightness = 0.45f;
    [SerializeField] private bool startNarrow = true;

    [Header("Beam Blocking")]
    [SerializeField] private LayerMask blockingLayers = ~0;
    [SerializeField, Range(32, 256)] private int occlusionRayCount = 256;
    [SerializeField, Min(0f)] private float hitPadding = 0.02f;
    [SerializeField] private bool blockTriggerColliders;

    [Header("Beam Visual")]
    [SerializeField, Range(0f, 1f)] private float beamOpacity = 0.42f;
    [SerializeField, Min(0f)] private float beamVisualIntensity = 2.2f;
    [SerializeField, Range(0.001f, 0.49f)] private float edgeSoftness = 0.12f;
    [SerializeField, Range(0.001f, 0.05f)] private float occlusionEdgeSoftness = 0.003f;
    [SerializeField, Range(0f, 0.25f)] private float shadowGlow = 0.08f;
    [SerializeField, Range(0f, 8f)] private float shadowGlowFalloff = 2.5f;    [SerializeField, Range(0f, 1f)] private float distanceFade = 0.55f;
    [SerializeField] private int beamSortingOrder = 20;

    [Header("Light Color")]
    [SerializeField] private Color primaryColor = new(1f, 0.88f, 0.55f, 1f);
    [SerializeField] private Color alternateColor = Color.cyan;
    [SerializeField, Range(0.0001f, 0.03f)] private float colorTransitionSoftness = 0.002f;

    private readonly RaycastHit2D[] raycastHits = new RaycastHit2D[RaycastHitCapacity];
    private Color currentColor;
    private float currentEndWidth;
    private float targetEndWidth;
    private float widthVelocity;

    private Vector2[] currentColliderPath;
    private Vector2[] currentRayStarts;
    private Vector2[] currentRayEnds;
    private float[] hitFractions;
    private Color[] colorProfilePixels;
    private readonly List<PrismLaserSegment> prismLaserSegments = new(16);
    private readonly List<LaserColorIntersection> laserColorIntersections = new(16);
    private struct LaserColorIntersection
    {
        public LaserColorIntersection(float y, Color color)
        {
            Y = y;
            Color = color;
        }

        public float Y { get; }
        public Color Color { get; }
    }

    private bool colliderPathDirty;
    private bool beamEnabled = true;

    private GameObject beamVisualObject;
    private Mesh beamMesh;
    private MeshRenderer beamMeshRenderer;
    private Material beamMaterial;
    private Texture2D occlusionTexture;
    private Texture2D colorProfileTexture;
    private readonly Vector3[] meshVertices = new Vector3[4];
    private readonly Vector2[] meshUvs =
    {
        new(0f, 0f), new(1f, 0f), new(1f, 1f), new(0f, 1f)
    };
    private readonly Color[] meshColors =
    {
        Color.white, Color.white, Color.white, Color.white
    };
    private readonly int[] meshTriangles = { 0, 1, 2, 0, 2, 3 };    public Color CurrentColor => currentColor;
    public float Spread => currentEndWidth;
    public float SpreadNormalized => Mathf.InverseLerp(narrowEndWidth, wideEndWidth, currentEndWidth);
    public float Brightness { get; private set; }
    public float BeamLength => beamLength;
    public float VerticalNormalized => Mathf.InverseLerp(minY, maxY, transform.localPosition.y);
    public bool IsNarrow { get; private set; }
    public bool IsVerticallyFixed => Mathf.Approximately(minY, maxY);
    public bool IsBeamEnabled => beamEnabled;

    /// <summary>Returns true for the player layer, which is intentionally transparent to every light ray.</summary>
    public static bool IsPlayerLightPassThrough(Collider2D collider)
    {
        int playerLayer = LayerMask.NameToLayer("PlayerNoBloom");
        return collider != null && playerLayer >= 0 && collider.gameObject.layer == playerLayer;
    }

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

        CreateProceduralBeamVisual();
        ApplyBeamGeometry(false);
    }

    private void Update()
    {
        UpdateWidthTransition();
    }

    private void LateUpdate()
    {
        ApplyBeamGeometry(true);
    }

    private void FixedUpdate()
    {
        ApplyLightAreaCollider();
    }

    private void OnDestroy()
    {
        if (beamMesh != null)
            Destroy(beamMesh);
        if (beamMaterial != null)
            Destroy(beamMaterial);
        if (occlusionTexture != null)
            Destroy(occlusionTexture);
        if (colorProfileTexture != null)
            Destroy(colorProfileTexture);
    }

    /// <summary>Enables or disables this light's visual and puzzle area without changing its configuration.</summary>
    public void SetBeamEnabled(bool enabled)
    {
        if (beamEnabled == enabled)
            return;

        beamEnabled = enabled;
        if (beamVisualObject != null)
            beamVisualObject.SetActive(beamEnabled);
        if (lightArea != null)
            lightArea.enabled = beamEnabled;

        StateChanged?.Invoke(this);
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
        UpdateBeamMaterial();
        StateChanged?.Invoke(this);
    }

    /// <summary>현재 충돌로 잘린 실제 빛 폴리곤 안에 지점이 있는지 검사한다.</summary>
    public bool IsPointLit(Vector2 worldPosition)
    {
        if (!beamEnabled || beamPivot == null || currentColliderPath == null)
            return false;

        Vector2 localPoint = beamPivot.InverseTransformPoint(worldPosition);
        return IsPointInsidePolygon(localPoint, currentColliderPath);
    }

    public bool Contains(Vector2 worldPosition) => IsPointLit(worldPosition);

    /// <summary>현재 직접광 판정 영역과 대상 콜라이더가 실제로 겹치는지 검사한다.</summary>
    public bool IsColliderLit(Collider2D target)
    {
        if (lightArea == null || target == null || !lightArea.enabled || !target.enabled)
            return false;

        ColliderDistance2D distance = Physics2D.Distance(lightArea, target);
        return distance.isValid && distance.isOverlapped;
    }

    /// <summary>
    /// Returns whether a collider is directly lit or is the surface that currently stops the beam.
    /// This is intended for light-reactive blockers such as the prism, whose surface is kept just
    /// outside the light-area collider by hitPadding.
    /// </summary>
    public bool IsColliderReceivingBeam(Collider2D target)
    {
        if (!beamEnabled || target == null || !target.enabled)
            return false;

        if (IsColliderLit(target))
            return true;

        if (beamPivot == null || currentRayEnds == null || hitFractions == null)
            return false;

        float contactTolerance = hitPadding + 0.005f;
        float contactToleranceSqr = contactTolerance * contactTolerance;

        for (int i = 0; i < currentRayEnds.Length; i++)
        {
            // A full-length ray did not collide with anything, so it cannot activate a blocker.
            if (hitFractions[i] >= 0.9999f)
                continue;

            Vector2 rayEnd = beamPivot.TransformPoint(currentRayEnds[i]);
            Vector2 closestPoint = target.ClosestPoint(rayEnd);
            if ((closestPoint - rayEnd).sqrMagnitude <= contactToleranceSqr)
                return true;
        }

        return false;
    }

    /// <summary>해당 지점의 현재 빛 색을 반환한다. 프리즘 레이저 통과 이후라면 변환된 색이다.</summary>
        public Color GetColorAtPoint(Vector2 worldPosition)
    {
        if (beamPivot == null || colorProfilePixels == null || currentRayStarts == null)
            return currentColor;

        Vector2 localPoint = beamPivot.InverseTransformPoint(worldPosition);
        float longitudinalT = Mathf.Clamp01(localPoint.y / Mathf.Max(beamLength, 0.0001f));
        float halfWidthAtPoint = Mathf.Lerp(sourceWidth * 0.5f, currentEndWidth * 0.5f, longitudinalT);
        float normalized = Mathf.InverseLerp(-halfWidthAtPoint, halfWidthAtPoint, localPoint.x);
        int column = Mathf.Clamp(Mathf.RoundToInt(normalized * (currentRayStarts.Length - 1)), 0, currentRayStarts.Length - 1);
        int row = Mathf.Clamp(Mathf.RoundToInt(longitudinalT * (ColorProfileRows - 1)), 0, ColorProfileRows - 1);
        Color profileColor = colorProfilePixels[row * currentRayStarts.Length + column];
        return profileColor.a > 0.5f ? profileColor : currentColor;
    }
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

        StateChanged?.Invoke(this);
    }

    private void ApplyBeamGeometry(bool applyOcclusion)
    {
        if (beamPivot == null)
            return;

        int sampleCount = Mathf.Max(32, occlusionRayCount);
        EnsureGeometryCapacity(sampleCount);

        float sourceHalfWidth = sourceWidth * 0.5f;
        float endHalfWidth = currentEndWidth * 0.5f;
        ContactFilter2D filter = CreateBlockingFilter();

        float leftBoundaryReachY = beamLength;
        float rightBoundaryReachY = beamLength;
        if (applyOcclusion)
        {
            leftBoundaryReachY = beamLength * GetBlockingCutoff(new Vector2(-sourceHalfWidth, 0f), new Vector2(-endHalfWidth, beamLength), filter);
            rightBoundaryReachY = beamLength * GetBlockingCutoff(new Vector2(sourceHalfWidth, 0f), new Vector2(endHalfWidth, beamLength), filter);
        }

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (sampleCount - 1f);
            float transverse = Mathf.Lerp(-endHalfWidth, endHalfWidth, t);
            float entryY = CalculateBeamEntryY(transverse, sourceHalfWidth, endHalfWidth);
            Vector2 rayStart = new(transverse, entryY);
            Vector2 rayTarget = new(transverse, beamLength);

            bool leftFeedBlocked = transverse < -sourceHalfWidth && entryY >= leftBoundaryReachY - 0.0001f;
            bool rightFeedBlocked = transverse > sourceHalfWidth && entryY >= rightBoundaryReachY - 0.0001f;
            float cutoff = leftFeedBlocked || rightFeedBlocked
                ? entryY / beamLength
                : applyOcclusion ? GetBlockingCutoff(rayStart, rayTarget, filter) : 1f;
            float clippedY = Mathf.Max(entryY, beamLength * cutoff);
            Vector2 rayEnd = new(transverse, clippedY);

            hitFractions[i] = Mathf.Clamp01(clippedY / beamLength);
            currentRayStarts[i] = rayStart;
            currentRayEnds[i] = rayEnd;
            currentColliderPath[i] = rayStart;
            currentColliderPath[currentColliderPath.Length - 1 - i] = rayEnd;
        }

        colliderPathDirty = true;
        if (!Application.isPlaying)
            ApplyLightAreaCollider();

        float widthRatio = Mathf.InverseLerp(narrowEndWidth, wideEndWidth, currentEndWidth);
        Brightness = Mathf.Lerp(narrowBrightness, wideBrightness, widthRatio);
        UpdateBeamMesh(sourceHalfWidth, endHalfWidth);
        UpdateColorConversionProfile(sampleCount, endHalfWidth);
        UploadOcclusionTexture(sampleCount);
        UploadColorProfileTexture(sampleCount);
        UpdateBeamMaterial();
    }

    private float CalculateBeamEntryY(float transverse, float sourceHalfWidth, float endHalfWidth)
    {
        float outsideSource = Mathf.Abs(transverse) - sourceHalfWidth;
        float expansion = endHalfWidth - sourceHalfWidth;
        if (outsideSource <= 0f || expansion <= 0.0001f)
            return 0f;

        return beamLength * Mathf.Clamp01(outsideSource / expansion);
    }    private void ApplyLightAreaCollider()
    {
        if (lightArea == null)
            return;

        lightArea.enabled = beamEnabled;
        if (!beamEnabled || !colliderPathDirty || !IsRenderablePolygon(currentColliderPath))
            return;

        lightArea.pathCount = 1;
        lightArea.SetPath(0, currentColliderPath);
        colliderPathDirty = false;
    }


    private float GetBlockingCutoff(Vector2 sourceLocal, Vector2 targetLocal, ContactFilter2D filter)
    {
        Vector2 worldStart = beamPivot.TransformPoint(sourceLocal);
        Vector2 worldEnd = beamPivot.TransformPoint(targetLocal);
        Vector2 rayVector = worldEnd - worldStart;
        float distance = rayVector.magnitude;
        if (distance < 0.0001f || blockingLayers.value == 0)
            return 1f;

        Vector2 direction = rayVector / distance;
        int hitCount = Physics2D.Raycast(worldStart, direction, filter, raycastHits, distance);
        float nearestDistance = distance;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hitCollider = raycastHits[i].collider;
            if (hitCollider == null || hitCollider == lightArea || IsPlayerLightPassThrough(hitCollider))
                continue;

            Transform hitTransform = hitCollider.transform;
            if (hitTransform == transform || hitTransform.IsChildOf(transform))
                continue;

            nearestDistance = Mathf.Min(nearestDistance, raycastHits[i].distance);
        }

        if (nearestDistance >= distance)
            return 1f;

        Vector2 paddedWorldHit = worldStart + direction * Mathf.Max(0f, nearestDistance - hitPadding);
        Vector2 paddedLocalHit = beamPivot.InverseTransformPoint(paddedWorldHit);
        return Mathf.Clamp01(paddedLocalHit.y / beamLength);
    }
    private ContactFilter2D CreateBlockingFilter()
    {
        var filter = new ContactFilter2D();
        filter.SetLayerMask(blockingLayers);
        filter.useTriggers = blockTriggerColliders;
        return filter;
    }

    private void EnsureGeometryCapacity(int sampleCount)
    {
        int pathLength = sampleCount * 2;
        if (currentColliderPath == null || currentColliderPath.Length != pathLength)
            currentColliderPath = new Vector2[pathLength];

        if (currentRayEnds != null && currentRayEnds.Length == sampleCount)
            return;

        currentRayStarts = new Vector2[sampleCount];
        currentRayEnds = new Vector2[sampleCount];
        hitFractions = new float[sampleCount];
        colorProfilePixels = new Color[sampleCount * ColorProfileRows];
        for (int i = 0; i < sampleCount; i++)
            hitFractions[i] = 1f;

        EnsureOcclusionTexture(sampleCount);
        EnsureColorProfileTexture(sampleCount);
    }

    private void CreateProceduralBeamVisual()
    {
        if (!Application.isPlaying || beamPivot == null)
            return;

        beamVisualObject = new GameObject("Procedural Beam Visual");
        beamVisualObject.transform.SetParent(beamPivot, false);
        beamVisualObject.transform.localPosition = Vector3.up * proceduralVisualOffset;
        beamVisualObject.transform.localRotation = Quaternion.identity;
        beamVisualObject.transform.localScale = Vector3.one;

        var meshFilter = beamVisualObject.AddComponent<MeshFilter>();
        beamMeshRenderer = beamVisualObject.AddComponent<MeshRenderer>();
        beamMeshRenderer.sortingOrder = beamSortingOrder;

        beamMesh = new Mesh { name = "Main Light Beam Mesh" };
        beamMesh.MarkDynamic();
        meshFilter.sharedMesh = beamMesh;

        Shader selectedShader = beamShader != null
            ? beamShader
            : Shader.Find("HANSEITHON/BeamGlow");
        if (selectedShader == null)
        {
            Debug.LogError("BeamGlow shader를 찾을 수 없습니다.", this);
            beamVisualObject.SetActive(false);
            return;
        }

        beamMaterial = new Material(selectedShader) { name = "Main Light Beam Material (Runtime)" };
        beamMeshRenderer.sharedMaterial = beamMaterial;
        UpdateBeamMaterial();
    }

    private void EnsureOcclusionTexture(int sampleCount)
    {
        if (!Application.isPlaying || (occlusionTexture != null && occlusionTexture.width == sampleCount))
            return;

        if (occlusionTexture != null)
            Destroy(occlusionTexture);

        occlusionTexture = new Texture2D(sampleCount, 1, TextureFormat.RFloat, false, true)
        {
            name = "Main Light Occlusion Profile",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
    }
    private void EnsureColorProfileTexture(int sampleCount)
    {
        if (!Application.isPlaying || (colorProfileTexture != null && colorProfileTexture.width == sampleCount && colorProfileTexture.height == ColorProfileRows))
            return;

        if (occlusionTexture != null)
            Destroy(occlusionTexture);
        if (colorProfileTexture != null)
            Destroy(colorProfileTexture);

        colorProfileTexture = new Texture2D(sampleCount, ColorProfileRows, TextureFormat.RGBAFloat, false, true)
        {
            name = "Main Light Color Conversion Profile",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
    }

    private void UpdateBeamMesh(float sourceHalfWidth, float endHalfWidth)
    {
        if (beamVisualObject != null)
            beamVisualObject.transform.localPosition = Vector3.up * proceduralVisualOffset;

        if (beamMesh == null)
            return;

        meshVertices[0] = new Vector3(-sourceHalfWidth, 0f, 0f);
        meshVertices[1] = new Vector3(sourceHalfWidth, 0f, 0f);
        meshVertices[2] = new Vector3(endHalfWidth, beamLength, 0f);
        meshVertices[3] = new Vector3(-endHalfWidth, beamLength, 0f);

        beamMesh.Clear();
        beamMesh.vertices = meshVertices;
        beamMesh.uv = meshUvs;
        beamMesh.colors = meshColors;
        beamMesh.triangles = meshTriangles;
        beamMesh.RecalculateBounds();
    }
    private void UpdateColorConversionProfile(int sampleCount, float endHalfWidth)
    {
        prismLaserSegments.Clear();
        PrismController.AppendEmittingLaserSegments(prismLaserSegments);
        ColorChangeLaser.AppendActiveLaserSegments(prismLaserSegments);

        for (int column = 0; column < sampleCount; column++)
        {
            float t = column / (sampleCount - 1f);
            float transverse = Mathf.Lerp(-endHalfWidth, endHalfWidth, t);
            laserColorIntersections.Clear();

            for (int laserIndex = 0; laserIndex < prismLaserSegments.Count; laserIndex++)
            {
                PrismLaserSegment segment = prismLaserSegments[laserIndex];
                Vector2 localStart = beamPivot.InverseTransformPoint(segment.Start);
                Vector2 localEnd = beamPivot.InverseTransformPoint(segment.End);
                if (!TryGetLaserIntersectionY(transverse, localStart, localEnd, segment.HalfWidth, out float intersectionY))
                    continue;
                // Outer fan rays begin farther down the beam than the narrow source.
                // A laser crossed before that visual edge appears still colors the light that expands into it.
                if (intersectionY > currentRayEnds[column].y + 0.0001f)
                    continue;

                laserColorIntersections.Add(new LaserColorIntersection(intersectionY, segment.Color));
            }

            laserColorIntersections.Sort(CompareLaserIntersections);
            int nextLaser = 0;
            bool hasLaserColor = false;
            Color accumulatedColor = currentColor;

            for (int row = 0; row < ColorProfileRows; row++)
            {
                float y = beamLength * row / (ColorProfileRows - 1f);
                while (nextLaser < laserColorIntersections.Count && laserColorIntersections[nextLaser].Y <= y)
                {
                    Color laserColor = laserColorIntersections[nextLaser].Color;
                    accumulatedColor = hasLaserColor ? BlendLaserColors(accumulatedColor, laserColor) : laserColor;
                    hasLaserColor = true;
                    nextLaser++;
                }

                colorProfilePixels[row * sampleCount + column] = hasLaserColor
                    ? new Color(accumulatedColor.r, accumulatedColor.g, accumulatedColor.b, 1f)
                    : Color.clear;
            }
        }
    }

    private static int CompareLaserIntersections(LaserColorIntersection left, LaserColorIntersection right)
    {
        return left.Y.CompareTo(right.Y);
    }

    private static Color BlendLaserColors(Color current, Color incoming)
    {
        return new Color((current.r + incoming.r) * 0.5f, (current.g + incoming.g) * 0.5f, (current.b + incoming.b) * 0.5f, 1f);
    }
private static bool TryGetLaserIntersectionY(
        float transverse,
        Vector2 start,
        Vector2 end,
        float halfWidth,
        out float intersectionY)
    {
        float minimumX = Mathf.Min(start.x, end.x) - halfWidth;
        float maximumX = Mathf.Max(start.x, end.x) + halfWidth;
        if (transverse < minimumX || transverse > maximumX)
        {
            intersectionY = 0f;
            return false;
        }

        float deltaX = end.x - start.x;
        if (Mathf.Abs(deltaX) < 0.0001f)
        {
            if (Mathf.Abs(transverse - start.x) > halfWidth)
            {
                intersectionY = 0f;
                return false;
            }

            intersectionY = Mathf.Min(start.y, end.y);
            return true;
        }

        float progress = Mathf.Clamp01((transverse - start.x) / deltaX);
        intersectionY = Mathf.Lerp(start.y, end.y, progress);
        return true;
    }

    private void UploadOcclusionTexture(int sampleCount)
    {
        EnsureOcclusionTexture(sampleCount);
        if (occlusionTexture == null)
            return;

        occlusionTexture.SetPixelData(hitFractions, 0);
        occlusionTexture.Apply(false, false);
    }
    private void UploadColorProfileTexture(int sampleCount)
    {
        EnsureColorProfileTexture(sampleCount);
        if (colorProfileTexture == null)
            return;

        colorProfileTexture.SetPixelData(colorProfilePixels, 0);
        colorProfileTexture.Apply(false, false);
    }

    private void UpdateBeamMaterial()
    {
        if (beamMaterial == null)
            return;

        beamMaterial.SetColor("_TintColor", currentColor);
        beamMaterial.SetFloat("_BeamOpacity", beamOpacity);
        beamMaterial.SetFloat("_Intensity", Brightness * beamVisualIntensity);
        beamMaterial.SetFloat("_EdgeSoftness", edgeSoftness);
        beamMaterial.SetFloat("_OcclusionSoftness", occlusionEdgeSoftness);
        beamMaterial.SetFloat("_ShadowGlow", shadowGlow);
        beamMaterial.SetFloat("_ShadowGlowFalloff", shadowGlowFalloff);        beamMaterial.SetFloat("_DistanceFade", distanceFade);
        beamMaterial.SetFloat("_EndHalfWidth", currentEndWidth * 0.5f);
        beamMaterial.SetFloat("_ColorTransitionSoftness", colorTransitionSoftness);
        if (occlusionTexture != null)
            beamMaterial.SetTexture("_OcclusionTex", occlusionTexture);        if (colorProfileTexture != null)
            beamMaterial.SetTexture("_ColorProfileTex", colorProfileTexture);
    }

    private static bool IsRenderablePolygon(Vector2[] polygon)
    {
        if (polygon == null || polygon.Length < 3)
            return false;

        float twiceArea = 0f;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            twiceArea += polygon[j].x * polygon[i].y - polygon[i].x * polygon[j].y;

        return Mathf.Abs(twiceArea) > 0.001f;
    }

    private static bool IsPointInsidePolygon(Vector2 point, Vector2[] polygon)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            Vector2 a = polygon[i];
            Vector2 b = polygon[j];
            bool crosses = (a.y > point.y) != (b.y > point.y)
                && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x;
            if (crosses)
                inside = !inside;
        }

        return inside;
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
        occlusionRayCount = Mathf.Clamp(occlusionRayCount, 32, 256);
        hitPadding = Mathf.Max(0f, hitPadding);
        proceduralVisualOffset = Mathf.Max(0f, proceduralVisualOffset);
        colorTransitionSoftness = Mathf.Clamp(colorTransitionSoftness, 0.0001f, 0.03f);

        CacheReferences();
        ApplyFixedDirection();

        currentColor = primaryColor;
        IsNarrow = startNarrow;
        targetEndWidth = IsNarrow ? narrowEndWidth : wideEndWidth;
        currentEndWidth = targetEndWidth;

        if (!Application.isPlaying)
            ApplyBeamGeometry(false);
        else
            UpdateBeamMaterial();
    }
}
