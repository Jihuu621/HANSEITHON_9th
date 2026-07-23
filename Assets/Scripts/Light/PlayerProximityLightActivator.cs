using UnityEngine;

/// <summary>
/// Keeps a fixed light off until the player deliberately turns it on from nearby.
/// </summary>
[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
public sealed class PlayerProximityLightActivator : MonoBehaviour
{
    [SerializeField] private MainLightController lightSource;
    [SerializeField, Min(0.1f)] private float activationRadius = 2.5f;
    [SerializeField] private bool startsActivated;

    [Header("Interaction")]
    [SerializeField] private KeyCode interactionKey = KeyCode.F;
    [SerializeField] private string interactionPrompt = "F  LIGHT ON";

    private Transform player;
    private bool activated;
    private bool stageSuppressed;
    private bool playerInRange;
    private GUIStyle promptStyle;

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
        playerInRange = false;
        if (stageSuppressed || activated || lightSource == null)
            return;

        ResolvePlayer();
        if (player == null)
            return;

        playerInRange = (player.position - transform.position).sqrMagnitude <= activationRadius * activationRadius;
        if (!playerInRange || !Input.GetKeyDown(interactionKey))
            return;

        activated = true;
        lightSource.SetBeamEnabled(true);
        RuntimeSfx.PlayLightOn();
    }

    private void OnGUI()
    {
        if (!playerInRange || activated || stageSuppressed || Camera.main == null)
            return;

        Vector3 screenPosition = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 0.85f);
        if (screenPosition.z < 0f)
            return;

        if (promptStyle == null)
        {
            promptStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 32,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.88f, 0.35f, 1f) }
            };
        }

        const float width = 260f;
        const float height = 68f;
        Rect popup = new(screenPosition.x - width * 0.5f, Screen.height - screenPosition.y - height, width, height);
        GUI.Box(popup, GUIContent.none);
        GUI.Label(popup, interactionPrompt, promptStyle);
    }

    public void SetStageSuppressed(bool suppressed)
    {
        stageSuppressed = suppressed;
        if (stageSuppressed)
        {
            activated = false;
            playerInRange = false;
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