using UnityEngine;

public class StageCeiling : MonoBehaviour
{
    private Collider2D ceilingCollider;


    private void Awake()
    {
        ceilingCollider = GetComponent<Collider2D>();
    }


    public void Open()
    {
        ceilingCollider.enabled = false;

        Debug.Log("다음 스테이지 이동 가능");
    }
}