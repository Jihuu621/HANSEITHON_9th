using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using DG.Tweening;

public class TitleManager : MonoBehaviour
{
    [System.Serializable]
    public class DialogueData
    {
        [TextArea(2, 4)] public string text;
        public float delayAfterLine = 1.0f;
        public bool isNewLine = true;
    }

    [Header("UI 연결")]
    public Image fadeImage;      // 시작 시 검은 배경
    public Image flashCircle;   // 방금 만든 동그란 섬광 Image (Knob/Circle 스프라이트)
    public SanabiDoTweenText sanabiText;

    [Header("씬 이동 설정")]
    public string nextSceneName = "Game";

    [Header("대사 연출 시퀀스")]
    public List<DialogueData> dialogues = new List<DialogueData>()
    {
        new DialogueData { text = "야!!! 기상!!", delayAfterLine = 1.5f, isNewLine = false },
        new DialogueData { text = "네 담당구역 광원이 또 고장 났어, 바로 고치러 가야 하니 죽기 싫으면 빨리 도와!", delayAfterLine = 0.5f, isNewLine = true }
    };

    private bool isLoading = false;

    void Start()
    {
        // 시작할 때 섬광 원형 이미지는 크기를 0으로 해서 숨겨둠
        if (flashCircle != null)
        {
            flashCircle.transform.localScale = Vector3.zero;
            flashCircle.gameObject.SetActive(true);
        }
    }

    void Update()
    {
        if (isLoading) return;

        if (Input.anyKeyDown)
        {
            isLoading = true;
            StartCoroutine(TitleSequenceRoutine());
        }
    }

    private IEnumerator TitleSequenceRoutine()
    {
        // 1. 검은 화면 페이드
        yield return fadeImage.DOFade(1f, 0.8f).WaitForCompletion();

        // 2. 대사 연출
        if (sanabiText != null && dialogues.Count > 0)
        {
            sanabiText.ClearText();

            for (int i = 0; i < dialogues.Count; i++)
            {
                var data = dialogues[i];
                bool useNewLine = (i != 0) && data.isNewLine;

                sanabiText.AppendText(data.text, useNewLine);
                yield return new WaitUntil(() => !sanabiText.IsPlaying);
                yield return new WaitForSeconds(data.delayAfterLine);
            }
        }

        // 3. '팟!' 하고 화면을 삼키는 충격파 섬광 연출!
        if (flashCircle != null)
        {
            // 섬광 이미지의 위치를 텍스트 중앙 쪽으로 맞춤 (또는 화면 중앙)
            flashCircle.transform.position = sanabiText.transform.position;

            // 시퀀스로 타격감 생성
            Sequence flashSeq = DOTween.Sequence();

            // A. 아주 짧은 순간(0.12초) 동안 엄청나게 거대해지며(Scale 50배) 화면을 '팟!' 가림
            // Ease.OutExpo나 Ease.OutQuad를 써야 팡! 터지는 느낌이 납니다.
            flashSeq.Join(flashCircle.transform.DOScale(Vector3.one * 60f, 0.12f).SetEase(Ease.OutExpo));

            // B. 동시에 카메라/텍스트가 가볍게 '쿵!' 하고 셰이크
            if (sanabiText != null)
            {
                sanabiText.transform.DOShakePosition(0.2f, strength: 30f, vibrato: 50);
            }

            yield return flashSeq.WaitForCompletion();
        }

        // 0.05초 정적 후 씬 전환
        yield return new WaitForSeconds(0.05f);

        DOTween.KillAll();
        SceneManager.LoadScene(nextSceneName);
    }
}