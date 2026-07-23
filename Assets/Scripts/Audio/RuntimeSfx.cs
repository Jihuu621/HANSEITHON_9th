using UnityEngine;

/// <summary>Small runtime SFX player for gameplay events that do not need a scene AudioSource.</summary>
public static class RuntimeSfx
{
    private static AudioSource source;
    private static AudioClip lightOn;
    private static AudioClip stageClear;

    public static void PlayLightOn()
    {
        if (lightOn == null)
            lightOn = Resources.Load<AudioClip>("Sounds/lighton");
        Play(lightOn);
    }

    public static void PlayStageClear()
    {
        if (stageClear == null)
            stageClear = Resources.Load<AudioClip>("Sounds/stage_clear");
        Play(stageClear);
    }

    private static void Play(AudioClip clip)
    {
        if (clip == null)
        {
            Debug.LogWarning("Requested gameplay SFX could not be loaded from Resources.");
            return;
        }

        if (source == null)
        {
            GameObject audioObject = new("Runtime SFX");
            Object.DontDestroyOnLoad(audioObject);
            source = audioObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
        }

        source.PlayOneShot(clip);
    }
}