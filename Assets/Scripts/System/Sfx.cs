using UnityEngine;

// Ayuda para los efectos de sonido: cada evento tiene varias variaciones y se elige una al azar
// para que no suene siempre igual.
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
