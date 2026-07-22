using System.Collections;
using UnityEngine;
using TMPro;
using DG.Tweening;

public class SanabiDoTweenText : MonoBehaviour
{
    private TMP_Text textComponent;

    [Header("타자기 설정")]
    [SerializeField] private float typingSpeed = 0.05f;

    [Header("타격감 연출 설정")]
    [SerializeField] private float punchStrength = 0.15f;
    [SerializeField] private float shakeStrength = 5f;

    // 현재 텍스트가 출력 중인지 확인할 수 있는 프로퍼티
    public bool IsPlaying { get; private set; } = false;

    void Awake()
    {
        textComponent = GetComponent<TMP_Text>();
        textComponent.maxVisibleCharacters = 0;
    }

    public void PlayText(string content)
    {
        StopAllCoroutines();
        StartCoroutine(TypeTextRoutine(content));
    }

    private IEnumerator TypeTextRoutine(string content)
    {
        IsPlaying = true; // 재생 시작

        textComponent.text = content;
        textComponent.maxVisibleCharacters = 0;

        yield return null;

        textComponent.ForceMeshUpdate();
        int totalChars = textComponent.textInfo.characterCount;

        for (int i = 0; i < totalChars; i++)
        {
            textComponent.maxVisibleCharacters = i + 1;

            char c = textComponent.textInfo.characterInfo[i].character;

            if (!char.IsWhiteSpace(c) && c != '\n' && c != '\r')
            {
                transform.DOKill(true);
                transform.DOPunchScale(Vector3.one * punchStrength, 0.08f, 10, 1f);
                transform.DOShakePosition(0.06f, strength: shakeStrength, vibrato: 30);
            }

            yield return new WaitForSeconds(typingSpeed);
        }

        IsPlaying = false; // 재생 완료
    }
}