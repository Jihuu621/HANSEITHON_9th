using UnityEngine;
using TMPro;
using DG.Tweening;

public class PressAnyKeyEffect : MonoBehaviour
{
    private TextMeshProUGUI text;

    void Start()
    {
        text = GetComponent<TextMeshProUGUI>();

        transform.DOScale(1.08f, 1.2f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
        text.DOFade(0.3f, 1.2f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
    }
}