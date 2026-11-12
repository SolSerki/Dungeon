using UnityEngine;

// Continuous sound built from short variations: while Playing is true it chains random clips,
// overlapping the end of one with the start of the next a little so there are no gaps.
// The volume fades in and out smoothly, and optionally "breathes" (rises and falls) while playing.
public class SfxLoop : MonoBehaviour
{
    [SerializeField] private AudioClip[] _clips;
    [SerializeField, Range(0f, 1f)] private float _volume = 0.3f;
    [SerializeField] private float _overlap = 0.3f;      // seconds each clip overlaps the next
    [SerializeField] private float _fadeIn = 0.4f;       // seconds to reach full volume
    [SerializeField] private float _fadeOut = 0.4f;      // seconds to fade out on release
    [SerializeField, Range(0f, 1f)] private float _swellAmount = 0f; // 0 = steady, 1 = dips down to silence
    [SerializeField] private float _swellSpeed = 1.5f;   // "breathing" speed

    private AudioSource[] _sources;
    private int _next;
    private float _nextStartTime;
    private float _fade;  // 0 = silent, 1 = full volume

    public bool Playing { get; set; }

    private void Awake()
    {
        // Two sources taking turns: that way a clip can finish while the next one has already started
        _sources = new AudioSource[2];
        for (int i = 0; i < _sources.Length; i++)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            _sources[i] = source;
        }
    }

    private void OnDisable()
    {
        Playing = false;
        _fade = 0f;
        foreach (var source in _sources) source.Stop();
    }

    private void Update()
    {
        float speed = Playing ? 1f / Mathf.Max(_fadeIn, 0.01f) : 1f / Mathf.Max(_fadeOut, 0.01f);
        _fade = Mathf.MoveTowards(_fade, Playing ? 1f : 0f, speed * Time.deltaTime);

        if (_fade <= 0f)
        {
            foreach (var source in _sources) source.Stop();
            _nextStartTime = 0f; // when it plays again it starts right away
            return;
        }

        if (Playing && Time.time >= _nextStartTime) PlayNext();

        float swell = 1f - _swellAmount * 0.5f * (1f + Mathf.Sin(Time.time * _swellSpeed));
        float volume = _volume * _fade * swell;
        foreach (var source in _sources) source.volume = volume;
    }

    private void PlayNext()
    {
        var clip = Sfx.Random(_clips);
        if (clip == null) return;

        var source = _sources[_next];
        _next = (_next + 1) % _sources.Length;
        source.clip = clip;
        source.Play();
        _nextStartTime = Time.time + Mathf.Max(0.05f, clip.length - _overlap);
    }
}
