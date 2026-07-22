using UnityEngine;

/// <summary>
/// 직접광을 받는 동안 두 고정 지점을 왕복하는 2D 발판.
/// 산란광은 MainLightController의 판정 영역에 포함되지 않으므로 작동 조건에 영향을 주지 않는다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public sealed class LightReactiveMovingPlatform : MonoBehaviour
{
    [Header("Light Detection")]
    [SerializeField] private MainLightController lightSource;
    [SerializeField, Range(1, 15)] private int lightProbeSamples = 7;
    [SerializeField, Min(0f)] private float lightProbePadding = 0.06f;

    [Header("Path")]
    [SerializeField] private Transform pointA;
    [SerializeField] private Transform pointB;
    [SerializeField, Min(0.01f)] private float moveSpeed = 2.5f;
    [SerializeField, Min(0.001f)] private float arrivalDistance = 0.02f;
    [SerializeField] private bool startTowardPointB = true;
    [SerializeField] private bool snapToPointAOnAwake = true;

    [Header("Visual Feedback")]
    [SerializeField] private SpriteRenderer platformRenderer;
    [SerializeField] private Color unlitColor = new(0.22f, 0.27f, 0.3f, 1f);
    [SerializeField] private Color litColor = new(1f, 0.78f, 0.32f, 1f);

    private Rigidbody2D body;
    private Collider2D platformCollider;
    private Vector2 pointAWorld;
    private Vector2 pointBWorld;
    private bool movingTowardPointB;
    private bool pointsCached;

    public bool IsIlluminated { get; private set; }
    public Vector2 PointAWorld => pointAWorld;
    public Vector2 PointBWorld => pointBWorld;

    private void Reset()
    {
        CacheComponents();
        CacheChildReferences();
        ConfigureBody();
    }

    private void Awake()
    {
        CacheComponents();
        CacheChildReferences();
        ConfigureBody();
        CachePathPoints();

        movingTowardPointB = startTowardPointB;
        if (snapToPointAOnAwake)
        {
            body.position = pointAWorld;
            transform.position = pointAWorld;
        }

        SetVisualState(false);
    }

    private void FixedUpdate()
    {
        ResolveLightSource();
        IsIlluminated = EvaluateIllumination();
        SetVisualState(IsIlluminated);

        if (!IsIlluminated || !pointsCached)
            return;

        Vector2 target = movingTowardPointB ? pointBWorld : pointAWorld;
        Vector2 next = Vector2.MoveTowards(body.position, target, moveSpeed * Time.fixedDeltaTime);

        if ((next - target).sqrMagnitude <= arrivalDistance * arrivalDistance)
        {
            next = target;
            movingTowardPointB = !movingTowardPointB;
        }

        body.MovePosition(next);
    }

    private bool EvaluateIllumination()
    {
        if (lightSource == null)
            return false;

        if (platformCollider == null)
            return lightSource.IsPointLit(transform.position);

        Vector2 direction = lightSource.Direction.normalized;
        if (direction.sqrMagnitude < 0.0001f)
            return false;

        Bounds bounds = platformCollider.bounds;
        Vector2 perpendicular = new(-direction.y, direction.x);
        float forwardExtent = Mathf.Abs(direction.x) * bounds.extents.x
            + Mathf.Abs(direction.y) * bounds.extents.y;
        float sideExtent = Mathf.Abs(perpendicular.x) * bounds.extents.x
            + Mathf.Abs(perpendicular.y) * bounds.extents.y;
        Vector2 incomingFace = (Vector2)bounds.center - direction * (forwardExtent + lightProbePadding);

        int sampleCount = Mathf.Max(1, lightProbeSamples);
        for (int i = 0; i < sampleCount; i++)
        {
            float t = sampleCount == 1 ? 0.5f : i / (sampleCount - 1f);
            Vector2 probePoint = incomingFace + perpendicular * Mathf.Lerp(-sideExtent, sideExtent, t);
            if (lightSource.IsPointLit(probePoint))
                return true;
        }

        return false;
    }
    private void ResolveLightSource()
    {
        if (lightSource == null)
            lightSource = FindAnyObjectByType<MainLightController>();
    }

    private void CachePathPoints()
    {
        pointAWorld = pointA != null ? pointA.position : body.position;
        pointBWorld = pointB != null ? pointB.position : body.position + Vector2.up * 3f;
        pointsCached = true;
    }

    private void CacheComponents()
    {
        body = GetComponent<Rigidbody2D>();
        platformCollider = GetComponent<Collider2D>();
        if (platformRenderer == null)
            platformRenderer = GetComponentInChildren<SpriteRenderer>(true);
    }

    private void CacheChildReferences()
    {
        if (pointA == null)
            pointA = transform.Find("Point A");
        if (pointB == null)
            pointB = transform.Find("Point B");
    }

    private void ConfigureBody()
    {
        if (body == null)
            return;

        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.useFullKinematicContacts = true;
    }

    private void SetVisualState(bool illuminated)
    {
        if (platformRenderer != null)
            platformRenderer.color = illuminated ? litColor : unlitColor;
    }

    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0.01f, moveSpeed);
        arrivalDistance = Mathf.Max(0.001f, arrivalDistance);
        lightProbeSamples = Mathf.Clamp(lightProbeSamples, 1, 15);
        lightProbePadding = Mathf.Max(0f, lightProbePadding);
        CacheComponents();
        CacheChildReferences();
        ConfigureBody();
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 a = Application.isPlaying && pointsCached
            ? pointAWorld
            : pointA != null ? pointA.position : transform.position;
        Vector3 b = Application.isPlaying && pointsCached
            ? pointBWorld
            : pointB != null ? pointB.position : transform.position + Vector3.up * 3f;

        Gizmos.color = new Color(1f, 0.78f, 0.32f, 0.9f);
        Gizmos.DrawLine(a, b);
        Gizmos.DrawWireSphere(a, 0.12f);
        Gizmos.DrawWireSphere(b, 0.12f);

    }
}
