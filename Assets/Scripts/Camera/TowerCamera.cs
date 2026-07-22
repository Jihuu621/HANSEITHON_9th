using UnityEngine;

public class TowerCamera : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float smoothSpeed = 5f;
    [SerializeField] private float fixedXPosition; 

    private bool isLocked = false;
    private float lockedYPosition;    

    private void Start()
    {
        fixedXPosition = transform.position.x;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        float targetY;
        if (isLocked)
        {
            targetY = lockedYPosition;
        }
        else
        {
            targetY = target.position.y;
        }

        Vector3 desiredPosition = new Vector3(fixedXPosition, targetY, transform.position.z);
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
        transform.position = smoothedPosition;
    }

    public void LockCamera(float yPosition)
    {
        isLocked = true;
        lockedYPosition = yPosition;
    }

    public void UnlockCamera()
    {
        isLocked = false;
    }
}