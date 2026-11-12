using System.Collections;
using NavMeshPlus.Components;
using UnityEngine;

public class LockedDoor : MonoBehaviour
{
    [SerializeField] private Animator _animator;
    [SerializeField] private Collider2D _blocker;
    [SerializeField] private NavMeshSurface _navMesh;
    [SerializeField] private NavMeshModifier[] _navBlockers; // the ones on Leaf_Left and Leaf_Right

    [Header("Sounds")]
    [SerializeField] private AudioSource _audio;
    [SerializeField] private AudioClip[] _unlockSounds;   // when using the key
    [SerializeField, Range(0f, 1f)] private float _unlockVolume = 0.7f;

    private int _openHash;
    private bool _isOpen;

    private void Awake()
    {
        _openHash = Animator.StringToHash("_open");

        // The prefab can't store references to scene objects: look them up on start
        if (_navMesh == null) _navMesh = FindAnyObjectByType<NavMeshSurface>();
        if (_navBlockers == null || _navBlockers.Length == 0) _navBlockers = GetComponentsInChildren<NavMeshModifier>();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (_isOpen) return;

        var inventory = collision.gameObject.GetComponentInParent<Inventory>();
        if (inventory == null || !inventory.TryUseKey()) return;

        Open();
    }

    private void Open()
    {
        _isOpen = true;
        Sfx.PlayRandom(_audio, _unlockSounds, _unlockVolume);
        _blocker.enabled = false;
        _animator.SetTrigger(_openHash);

        StartCoroutine(RebuildNavMesh());
    }

    // Open() is called from OnCollisionEnter2D, and BuildNavMesh destroys temporary meshes,
    // which Unity doesn't allow inside a physics callback: wait for the next frame
    private IEnumerator RebuildNavMesh()
    {
        yield return null;

        foreach (var blocker in _navBlockers) blocker.ignoreFromBuild = true;
        // Builds a new NavMesh in memory; doesn't touch the scene's baked asset
        _navMesh.BuildNavMesh();
    }

}
