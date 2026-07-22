using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using DG.Tweening;

public class TitleManager : MonoBehaviour
{
    [Header("UI 연결")]
    public Image fadeImage;
    public SanabiDoTweenText sanabiText; // 방금 만든 텍스트 스크립트 연결

    [Header("설정")]
    public string nextSceneName = "Game";
    [TextArea] public string titleDialogue = "산나비 스타일의 대사가 나온 뒤 씬이 이동합니다.";
    public float waitAfterText = 1.0f; // 텍스트 출력이 끝난 후 잠시 대기할 시간

    private bool isLoading = false;

    void Update()
    {
        if (isLoading)
            return;

        if (Input.anyKeyDown)
        {
            isLoading = true;
            StartCoroutine(TitleSequenceRoutine());
        }
    }

    private IEnumerator TitleSequenceRoutine()
    {
        // 1. 화면 페이드 아웃 (검은색으로 어두워짐)
        yield return fadeImage.DOFade(1f, 1f).WaitForCompletion();

        // 2. 페이드 완료 후 산나비 텍스트 연출 시작
        if (sanabiText != null)
        {
            sanabiText.PlayText(titleDialogue);

            // 텍스트 출력이 완전히 끝날 때까지 대기
            yield return new WaitUntil(() => !sanabiText.IsPlaying);
        }

        // 3. 연출 완료 후 여운을 위한 잠시 대기
        yield return new WaitForSeconds(waitAfterText);
        DOTween.KillAll();
        // 4. 다음 씬으로 이동
        SceneManager.LoadScene(nextSceneName);
    }
}