using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Changes the left companion portrait according to the active stage's time pressure.</summary>
[DisallowMultipleComponent]
public sealed class CompanionMoodController : MonoBehaviour
{
    private const float UrgentThreshold = 0.5f;
    private const float CriticalThreshold = 0.2f;

    private static CompanionMoodController instance;
    private Image portrait;
    private Sprite normalSprite;
    private Sprite urgentSprite;
    private Sprite happySprite;
    private Coroutine clearRoutine;
    private float timeRatio = 1f;

    public static CompanionMoodController Ensure(StageManager host)
    {
        if (instance == null)
            instance = host.GetComponent<CompanionMoodController>();
        if (instance == null)
            instance = host.gameObject.AddComponent<CompanionMoodController>();
        return instance;
    }

    private void Awake()
    {
        instance = this;
        ResolvePortraitAndSprites();
        ApplyMood();
    }

    private void Update()
    {
        if (portrait == null || timeRatio > CriticalThreshold)
            return;

        float pulse = 0.9f + Mathf.PingPong(Time.unscaledTime * 5f, 0.1f);
        portrait.color = new Color(1f, 0.72f * pulse, 0.72f * pulse, 1f);
    }

    public void SetTimePressure(float normalizedRemainingTime)
    {
        if (clearRoutine != null)
            return;

        timeRatio = Mathf.Clamp01(normalizedRemainingTime);
        ApplyMood();
    }

    public void PlayClearMood()
    {
        if (clearRoutine != null)
            StopCoroutine(clearRoutine);
        clearRoutine = StartCoroutine(ShowClearMood());
    }

    private IEnumerator ShowClearMood()
    {
        ResolvePortraitAndSprites();
        if (portrait != null && happySprite != null)
        {
            portrait.sprite = happySprite;
            portrait.color = Color.white;
        }

        yield return new WaitForSecondsRealtime(1.5f);
        clearRoutine = null;
        timeRatio = 1f;
        ApplyMood();
    }

    private void ApplyMood()
    {
        ResolvePortraitAndSprites();
        if (portrait == null)
            return;

        portrait.sprite = timeRatio <= UrgentThreshold && urgentSprite != null ? urgentSprite : normalSprite;
        portrait.color = timeRatio <= CriticalThreshold
            ? new Color(1f, 0.72f, 0.72f, 1f)
            : Color.white;
    }

    private void ResolvePortraitAndSprites()
    {
        if (portrait == null)
        {
            Image[] images = FindObjectsByType<Image>(FindObjectsInactive.Include);
            portrait = images.FirstOrDefault(image => image != null && image.name == "Alpha");
        }

        if (normalSprite != null && urgentSprite != null && happySprite != null)
            return;

        Sprite[] alphaSprites = Resources.LoadAll<Sprite>("Sprites/Alpha").OrderBy(sprite => sprite.name).ToArray();
        if (alphaSprites.Length < 3)
            return;

        normalSprite = alphaSprites.FirstOrDefault(sprite => sprite.name == "Alpha_0") ?? alphaSprites[0];
        urgentSprite = alphaSprites.FirstOrDefault(sprite => sprite.name == "Alpha_1") ?? alphaSprites[1];
        happySprite = alphaSprites.FirstOrDefault(sprite => sprite.name == "Alpha_2") ?? alphaSprites[2];
    }
}