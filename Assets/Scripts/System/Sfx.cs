using UnityEngine;

// Sound effect helper: each event has several variations and one is picked at random
// so it doesn't always sound the same.
public static class Sfx
{
    public static AudioClip Random(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0) return null;
        return clips[UnityEngine.Random.Range(0, clips.Length)];
    }

    public static void PlayRandom(AudioSource source, AudioClip[] clips, float volume = 1f)
    {
        var clip = Random(clips);
        if (source != null && clip != null) source.PlayOneShot(clip, volume);
    }
}
