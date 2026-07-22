using UnityEngine;

public class CameraZone : MonoBehaviour
{
    [Header("고정할 카메라 Y 높이")]
    [SerializeField] private float targetCameraY;

    [Header("고정 높이를 이 오브젝트의 Y 위치로 쓸지 여부")]
    [SerializeField] private bool useThisObjectY = true;

    private void Awake()
    {
        if (useThisObjectY)
        {
            targetCameraY = transform.position.y;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            TowerCamera mainCam = Camera.main.GetComponent<TowerCamera>();
            if (mainCam != null)
            {
                mainCam.LockCamera(targetCameraY);
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            TowerCamera mainCam = Camera.main.GetComponent<TowerCamera>();
            if (mainCam != null)
            {
                mainCam.UnlockCamera();
            }
        }
    }
}