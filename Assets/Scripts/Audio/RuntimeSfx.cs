using UnityEngine;

/// <summary>Plays short gameplay sound effects without scene-bound AudioSources.</summary>
public static class RuntimeSfx
{
    private static AudioClip lightOn;
    private static AudioClip stageClear;
    private static AudioClip footstep;

    public static void PlayLightOn()
    {
        if (lightOn == null)
            lightOn = Resources.Load<AudioClip>("Sounds/lighton");
        Play(lightOn, 1f);
    }

    public static void PlayStageClear()
    {
        if (stageClear == null)
            stageClear = Resources.Load<AudioClip>("Sounds/stage_clear");
        Play(stageClear, 1.3f);
    }

    public static void PlayFootstep()
    {
        if (footstep == null)
            footstep = Resources.Load<AudioClip>("Sounds/step1");
        Play(footstep, Random.Range(0.94f, 1.06f));
    }

    private static void Play(AudioClip clip, float pitch)
    {
        if (clip == null)
        {
            Debug.LogWarning("Requested gameplay SFX could not be loaded from Resources.");
            return;
        }

        GameObject audioObject = new("Runtime SFX");
        Object.DontDestroyOnLoad(audioObject);
        AudioSource source = audioObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.pitch = pitch;
        source.clip = clip;
        source.Play();
        Object.Destroy(audioObject, clip.length / Mathf.Max(0.01f, pitch) + 0.05f);
    }
}