using UnityEngine;

public class StageManager : MonoBehaviour
{

    public static StageManager Instance;

    public StageCeiling ceiling;
    public StageTimer timer;

    private bool clear = false;


    private void Awake()
    {
        Instance = this;
    }

    public void PuzzleComplete()
    {
        if (clear)
            return;

        clear = true;


        if (timer != null)
        {
            timer.StopTimer();
        }


        if (ceiling != null)
        {
            ceiling.Open();
        }


        Debug.Log("스테이지 클리어");
    }



    public bool IsClear()
    {
        return clear;
    }

}