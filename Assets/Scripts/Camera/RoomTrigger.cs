using UnityEngine;
using Unity.Cinemachine; 

public class RoomTrigger : MonoBehaviour
{
    [Header("이 구역의 Virtual Camera")]
    public CinemachineCamera roomCamera;
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {          
            roomCamera.Priority = 10;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            roomCamera.Priority = 0;
        }
    }
}