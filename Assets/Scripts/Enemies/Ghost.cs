using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.AI;

// Final boss of Level 4. Attacks don't hurt it (it has no HealthSystem): only light does.
// It appears at a random spot of its area, floats there and revives the dead Slime2s
// (Revivable) inside the area. A slime chunk landing next to it banishes it for a while;
// the 7th time it doesn't come back.
public class Ghost : MonoBehaviour
{
    private enum State { Dormant, Hidden, Appearing, Floating, Defeated }

    [SerializeField] private Animator _animator;
    [SerializeField] private Collider2D _body;      // where the chunk's light has to reach
    [SerializeField] private BoxCollider2D _area;   // where it appears and which corpses it revives. Only its shape
                                                    // is used (the component stays disabled), read once in Awake

    [Header("Appearing")]
    [SerializeField] private float _startDelay = 1f;          // after the player walks into the area
    [SerializeField] private float _appearDuration = 1f;      // Ghost_appear length
    [SerializeField] private float _hiddenTime = 10f;         // time away after each banishment
    [SerializeField] private float _minPlayerDistance = 3f;   // doesn't appear on top of the player

    [Header("Light")]
    [SerializeField] private float _lightRadius = 0.75f;      // a chunk this close to its body banishes it
    [SerializeField] private int _banishesToDefeat = 7;

    [Header("Reviving")]
    [SerializeField] private float _reviveInterval = 4f;      // at most one Slime2 every this many seconds
    [SerializeField] private float _corpseDelay = 3f;         // a corpse has to stay dead at least this long

    [Header("Defeat")]
    [SerializeField] private float _defeatDelay = 2f;
    [SerializeField] private GameObject[] _activateOnDefeat;  // e.g. the level exit

    [Header("Feedback")]
    [SerializeField] private CinemachineImpulseSource _impulse;
    [SerializeField] private float _banishShake = 0.4f;
    [SerializeField] private AudioSource _audio;
    [SerializeField] private AudioClip[] _appearSounds;
    [SerializeField] private AudioClip[] _banishSounds;
    [SerializeField] private AudioClip[] _reviveSounds;
    [SerializeField] private AudioClip[] _defeatSounds;
    [SerializeField, Range(0f, 1f)] private float _volume = 0.7f;

    private Transform _player;
    private Rect _areaRect;
    private State _state;
    private float _stateEndTime;
    private float _nextReviveTime;
    private int _banishCount;
    private int _appearHash, _disappearHash, _isDeadHash;

    public int BanishCount => _banishCount;
    public int BanishesToDefeat => _banishesToDefeat;

    // For the boss bar (BossHealthBar)
    public event System.Action OnAwakened;            // the player walked into the area: the fight starts
    public event System.Action<int, int> OnBanished;  // (banishes so far, banishes to defeat), also the last one
    public event System.Action OnDefeated;

    private void Awake()
    {
        _appearHash = Animator.StringToHash("_appear");
        _disappearHash = Animator.StringToHash("_disappear");
        _isDeadHash = Animator.StringToHash("_isDead");

        // The area may be a child: it is read now, before the ghost starts moving around
        Vector2 center = _area.transform.TransformPoint(_area.offset);
        Vector2 size = Vector2.Scale(_area.size, _area.transform.lossyScale);
        _areaRect = new Rect(center - size / 2f, size);
    }

    private void Start()
    {
        _player = GameObject.FindWithTag("Player").transform;
        _state = State.Dormant; // invisible (Ghost_hidden) until the player walks in
    }

    private void Update()
    {
        switch (_state)
        {
            case State.Dormant:
                if (_areaRect.Contains(_player.position))
                {
                    Hide(_startDelay);
                    OnAwakened?.Invoke();
                }
                break;

            case State.Hidden:
                if (Time.time >= _stateEndTime) Appear();
                break;

            case State.Appearing:
                if (IsLitAt(transform.position)) Banish();
                else if (Time.time >= _stateEndTime)
                {
                    _state = State.Floating;
                    _nextReviveTime = Time.time + _reviveInterval;
                }
                break;

            case State.Floating:
                if (IsLitAt(transform.position)) Banish();
                else if (Time.time >= _nextReviveTime) TryRevive();
                break;
        }
    }

    private void Hide(float duration)
    {
        _state = State.Hidden;
        _stateEndTime = Time.time + duration;
    }

    private void Appear()
    {
        transform.position = PickSpot();
        Physics2D.SyncTransforms(); // so _body.bounds is already at the new spot this frame
        _animator.ResetTrigger(_disappearHash);
        _animator.SetTrigger(_appearHash);
        Sfx.PlayRandom(_audio, _appearSounds, _volume);
        _state = State.Appearing;
        _stateEndTime = Time.time + _appearDuration;
    }

    private void Banish()
    {
        _banishCount++;
        if (_impulse != null) _impulse.GenerateImpulseWithVelocity(Vector3.down * _banishShake);
        OnBanished?.Invoke(_banishCount, _banishesToDefeat);

        if (_banishCount >= _banishesToDefeat)
        {
            StartCoroutine(Defeat());
            return;
        }

        Sfx.PlayRandom(_audio, _banishSounds, _volume);
        _animator.ResetTrigger(_appearHash);
        _animator.SetTrigger(_disappearHash);
        Hide(_hiddenTime);
    }

    private IEnumerator Defeat()
    {
        _state = State.Defeated;
        _body.enabled = false;
        Sfx.PlayRandom(_audio, _defeatSounds, _volume);
        _animator.SetBool(_isDeadHash, true);
        OnDefeated?.Invoke();

        yield return new WaitForSeconds(_defeatDelay);
        foreach (var go in _activateOnDefeat)
            if (go != null) go.SetActive(true);
    }

    // Revives the Slime2 that has been dead the longest, if there is one ready inside the area
    private void TryRevive()
    {
        Revivable oldest = null;
        foreach (var corpse in Revivable.Dead)
        {
            if (Time.time - corpse.DiedAt < _corpseDelay) continue;
            if (!_areaRect.Contains(corpse.transform.position)) continue;
            if (oldest == null || corpse.DiedAt < oldest.DiedAt) oldest = corpse;
        }
        if (oldest == null) return; // none yet: it checks again next frame

        oldest.Revive();
        Sfx.PlayRandom(_audio, _reviveSounds, _volume);
        _nextReviveTime = Time.time + _reviveInterval;
    }

    // Random spot in the area: over the floor (the NavMesh, so not in a wall, a pillar or a corner
    // of the rectangle outside the room), away from the player and out of any chunk's light
    private Vector2 PickSpot()
    {
        Vector2 fallback = transform.position;
        for (int i = 0; i < 30; i++)
        {
            var spot = new Vector2(Random.Range(_areaRect.xMin, _areaRect.xMax), Random.Range(_areaRect.yMin, _areaRect.yMax));
            if (!NavMesh.SamplePosition(spot, out var hit, 0.5f, NavMesh.AllAreas)) continue;
            spot = hit.position;
            fallback = spot;
            bool farFromPlayer = Vector2.Distance(spot, _player.position) >= _minPlayerDistance;
            if (farFromPlayer && !IsLitAt(spot)) return spot;
        }
        return fallback; // no perfect spot found: the last floor spot tried will do
    }

    // Is there a chunk on the floor whose light would reach the body with the ghost standing at 'position'?
    private bool IsLitAt(Vector2 position)
    {
        Bounds body = _body.bounds;
        Vector2 offset = position - (Vector2)transform.position;
        Vector2 margin = Vector2.one * _lightRadius;
        var lit = new Rect((Vector2)body.min + offset - margin, (Vector2)body.size + margin * 2f);

        foreach (var chunk in SlimeChunk.Active)
            if (chunk.IsLanded && lit.Contains(chunk.transform.position)) return true;
        return false;
    }

    private void OnDrawGizmosSelected()
    {
        if (_area == null) return;
        Gizmos.color = new Color(0.6f, 0.8f, 1f);
        Vector2 center = Application.isPlaying ? _areaRect.center : (Vector2)_area.transform.TransformPoint(_area.offset);
        Vector2 size = Application.isPlaying ? _areaRect.size : Vector2.Scale(_area.size, _area.transform.lossyScale);
        Gizmos.DrawWireCube(center, size);
    }
}
