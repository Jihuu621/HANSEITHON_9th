using System.Collections;
using UnityEngine;

/// <summary>Moves a stage floor as a kinematic elevator so the player can ride it upward.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class StageElevator : MonoBehaviour
{
    private Rigidbody2D body;

    private void Awake()
    {
        CacheAndConfigureBody();
    }

    public IEnumerator MoveTo(Vector3 destination, float duration)
    {
        CacheAndConfigureBody();

        Vector3 start = transform.position;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            yield return new WaitForFixedUpdate();
            elapsed += Time.fixedDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            Vector3 next = Vector3.LerpUnclamped(start, destination, t);
            body.MovePosition(next);
        }

        body.position = destination;
    }

    private void CacheAndConfigureBody()
    {
        if (body == null)
            body = GetComponent<Rigidbody2D>();
        if (body == null)
            body = gameObject.AddComponent<Rigidbody2D>();

        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.useFullKinematicContacts = true;
    }
}