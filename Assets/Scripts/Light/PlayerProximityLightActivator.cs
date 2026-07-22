using UnityEngine;

/// <summary>
/// Keeps a fixed light off until the player enters its activation radius, then latches it on.
/// </summary>
[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
public sealed class PlayerProximityLightActivator : MonoBehaviour
{
    [SerializeField] private MainLightController lightSource;
    [SerializeField, Min(0.1f)] private float activationRadius = 2.5f;
    [SerializeField] private bool startsActivated;

    private Transform player;
    private bool activated;
    private bool stageSuppressed;

    private void Awake()
    {
        if (lightSource == null)
            lightSource = GetComponent<MainLightController>();

        activated = startsActivated;
        if (lightSource != null)
            lightSource.SetBeamEnabled(activated);
    }

    private void Update()
    {
        if (stageSuppressed || activated || lightSource == null)
            return;

        ResolvePlayer();
        if (player == null)
            return;

        if ((player.position - transform.position).sqrMagnitude > activationRadius * activationRadius)
            return;

        activated = true;
        lightSource.SetBeamEnabled(true);
    }

    public void SetStageSuppressed(bool suppressed)
    {
        stageSuppressed = suppressed;
        if (stageSuppressed)
        {
            activated = false;
            if (lightSource != null)
                lightSource.SetBeamEnabled(false);
        }
    }

    private void ResolvePlayer()
    {
        if (player != null)
            return;

        PlayerController controller = FindAnyObjectByType<PlayerController>();
        if (controller != null)
            player = controller.transform;
    }

    private void OnValidate()
    {
        activationRadius = Mathf.Max(0.1f, activationRadius);
        if (lightSource == null)
            lightSource = GetComponent<MainLightController>();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = activated ? new Color(1f, 0.8f, 0.25f, 0.8f) : new Color(0.35f, 0.75f, 1f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, activationRadius);
    }
}