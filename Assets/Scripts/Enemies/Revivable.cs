using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Lets a Ghost bring this enemy back to life (Slime2).
// While dead it is listed in Dead. Revive() plays the death animation backwards and then
// gives back its health: HealthSystem.OnRevived turns the AI, movement and body back on.
[RequireComponent(typeof(HealthSystem))]
public class Revivable : MonoBehaviour
{
    [SerializeField] private Animator _animator;
    [SerializeField] private float _reviveDuration = 10f / 12f; // length of the Slime2_revive clips

    // Dead enemies waiting to be revived: the Ghost looks them up here instead of searching the scene
    private static readonly List<Revivable> _dead = new();
    public static IReadOnlyList<Revivable> Dead => _dead;

    private HealthSystem _health;
    private int _isDeadHash;
    private int _reviveHash;

    public float DiedAt { get; private set; }

    // The project has Domain Reload disabled: static lists survive between plays unless they are cleared
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearStatics() => _dead.Clear();

    private void Awake()
    {
        _health = GetComponent<HealthSystem>();
        _isDeadHash = Animator.StringToHash("_isDead");
        _reviveHash = Animator.StringToHash("_revive");
        _health.OnDied += HandleDied;
    }

    private void OnDestroy()
    {
        _health.OnDied -= HandleDied;
        _dead.Remove(this);
    }

    private void HandleDied()
    {
        DiedAt = Time.time;
        _dead.Add(this);
    }

    public void Revive()
    {
        if (!_dead.Remove(this)) return; // not dead, or already being revived
        StartCoroutine(ReviveRoutine());
    }

    private IEnumerator ReviveRoutine()
    {
        _animator.SetBool(_isDeadHash, false); // otherwise Any State -> death would catch it again
        _animator.SetTrigger(_reviveHash);
        yield return new WaitForSeconds(_reviveDuration);
        _health.Revive(); // it can be hit again only once it has re-formed
    }
}
