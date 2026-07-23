using System.Collections;
using UnityEngine;
using TMPro;
using DG.Tweening;

public class SanabiDoTweenText : MonoBehaviour
{
    private TMP_Text textComponent;

    [Header("타자기 설정")]
    [SerializeField] private float typingSpeed = 0.04f; // 타이핑 속도

    [Header("타격감 연출 설정")]
    [SerializeField] private float punchStrength = 0.15f; // 커지는 강도
    [SerializeField] private float shakeStrength = 5f;    // 흔들림 강도

    public bool IsPlaying { get; private set; } = false;

    void Awake()
    {
        textComponent = GetComponent<TMP_Text>();
        ClearText();
    }

    private void OnDisable() => transform.DOKill();
    private void OnDestroy() => transform.DOKill();

    // 텍스트 초기화 함수
    public void ClearText()
    {
        if (textComponent == null) textComponent = GetComponent<TMP_Text>();
        textComponent.text = "";
        textComponent.maxVisibleCharacters = 0;
    }

    // 텍스트 덧붙이기 (줄바꿈 및 타자기 연출)
    public void AppendText(string newContent, bool addNewLine = true)
    {
        StartCoroutine(AppendTextRoutine(newContent, addNewLine));
    }

    private IEnumerator AppendTextRoutine(string newContent, bool addNewLine)
    {
        IsPlaying = true;

        // 기존에 찍혀있던 글자 수 기억
        int startVisibleIndex = textComponent.textInfo.characterCount;

        // 줄바꿈 여부에 따라 텍스트 결합
        if (addNewLine && !string.IsNullOrEmpty(textComponent.text))
        {
            textComponent.text += "\n" + newContent;
        }
        else
        {
            textComponent.text += newContent;
        }

        textComponent.ForceMeshUpdate();
        int totalChars = textComponent.textInfo.characterCount;

        // 새로 추가된 글자 부분부터 타자기 연출 시작
        for (int i = startVisibleIndex; i < totalChars; i++)
        {
            textComponent.maxVisibleCharacters = i + 1;

            char c = textComponent.textInfo.characterInfo[i].character;

            // 공백 및 줄바꿈이 아닐 때만 펀치/흔들림 적용
            if (!char.IsWhiteSpace(c) && c != '\n' && c != '\r')
            {
                transform.DOKill(true);
                transform.DOPunchScale(Vector3.one * punchStrength, 0.08f, 10, 1f);
                transform.DOShakePosition(0.06f, strength: shakeStrength, vibrato: 30);
            }

            yield return new WaitForSeconds(typingSpeed);
        }

        IsPlaying = false;
    }
}