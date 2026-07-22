using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class StageCeiling : MonoBehaviour
{
    private Collider2D ceilingCollider;
    private Renderer[] visualRenderers;

    private void Awake()
    {
        ceilingCollider = GetComponent<Collider2D>();
        visualRenderers = GetComponentsInChildren<Renderer>(true);
    }

    public void Open()
    {
        if (ceilingCollider != null)
            ceilingCollider.enabled = false;

        for (int i = 0; i < visualRenderers.Length; i++)
        {
            if (visualRenderers[i] != null)
                visualRenderers[i].enabled = false;
        }

        Debug.Log("Next vertical stage path opened.", this);
    }
}