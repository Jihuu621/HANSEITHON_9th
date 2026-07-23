using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class StageTimerFrameBinder
{
    static StageTimerFrameBinder()
    {
        EditorApplication.delayCall += BindExistingTimerFrame;
        EditorApplication.hierarchyChanged += BindExistingTimerFrame;
        EditorSceneManager.sceneOpened += (_, _) => BindExistingTimerFrame();
    }

    private static void BindExistingTimerFrame()
    {
        if (Application.isPlaying || !SceneManager.GetActiveScene().isLoaded)
            return;

        TMP_Text target = FindTimerTextInGameUi();
        if (target == null)
            return;

        bool changed = false;
        foreach (StageTimer timer in Object.FindObjectsByType<StageTimer>(FindObjectsInactive.Include))
        {
            if (timer.timerText == target)
                continue;

            Undo.RecordObject(timer, "Bind Existing Timer Frame");
            timer.timerText = target;
            EditorUtility.SetDirty(timer);
            changed = true;
        }

        if (changed)
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    private static TMP_Text FindTimerTextInGameUi()
    {
        foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include))
        {
            if (canvas.name != "GameUI")
                continue;

            TMP_Text[] texts = canvas.GetComponentsInChildren<TMP_Text>(true);
            if (texts.Length == 1)
                return texts[0];

            foreach (TMP_Text text in texts)
            {
                string name = text.name.ToLowerInvariant();
                if (name.Contains("timer") || name.Contains("time") || name.Contains("clock"))
                    return text;
            }
        }

        return null;
    }
}