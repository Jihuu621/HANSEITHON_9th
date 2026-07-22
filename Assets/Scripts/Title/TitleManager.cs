using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using DG.Tweening;

public class TitleManager : MonoBehaviour
{
    public Image fadeImage;
    public string nextSceneName = "Game";

    bool isLoading = false;

    void Update()
    {
        if (isLoading)
            return;

        if (Input.anyKeyDown)
        {
            isLoading = true;

            fadeImage.DOFade(1f, 1f).OnComplete(() =>
            {
                SceneManager.LoadScene(nextSceneName);
            });
        }
    }
}