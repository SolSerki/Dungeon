using System.Collections;
using NavMeshPlus.Components;
using UnityEngine;

public class LockedDoor : MonoBehaviour
{
    [SerializeField] private Animator _animator;
    [SerializeField] private Collider2D _blocker;
    [SerializeField] private NavMeshSurface _navMesh;
    [SerializeField] private NavMeshModifier[] _navBlockers; // los de Hoja_iz y Hoja_de

    private int _openHash;
    private bool _isOpen;

    private void Awake()
    {
        _openHash = Animator.StringToHash("_open");

        // El prefab no puede guardar referencias a objetos de la escena: las buscamos al empezar
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
        _blocker.enabled = false;
        _animator.SetTrigger(_openHash);

        StartCoroutine(RebuildNavMesh());
    }

    // Open() se llama desde OnCollisionEnter2D, y BuildNavMesh destruye mallas temporales,
    // algo que Unity no permite dentro de un callback de física: esperamos al frame siguiente
    private IEnumerator RebuildNavMesh()
    {
        yield return null;

        foreach (var blocker in _navBlockers) blocker.ignoreFromBuild = true;
        // Genera un NavMesh nuevo en memoria; no toca el asset horneado de la escena
        _navMesh.BuildNavMesh();
    }

}
