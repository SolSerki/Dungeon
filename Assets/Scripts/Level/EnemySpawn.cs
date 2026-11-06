using System;
using UnityEngine;

// Marcador para el Level Builder: indica qué enemigo va acá.
// Sus hijos son los waypoints de patrulla, en orden.
public class EnemySpawn : MonoBehaviour
{
    [Serializable]
    public struct Variant
    {
        public GameObject Prefab;
        [Min(0f)] public float Weight;
    }

    [SerializeField] private GameObject _prefab;
    [Tooltip("Opcional: si hay variantes, el Level Builder sortea una según su peso en lugar de usar Prefab")]
    [SerializeField] private Variant[] _variants = new Variant[0];
    [SerializeField, HideInInspector] private int _seed;          // fija el sorteo para que no cambie en cada Build
    [SerializeField, HideInInspector] private GameObject _instance; // lo asigna el Level Builder
    [SerializeField, HideInInspector] private GameObject[] _loot = new GameObject[0]; // se edita con los toggles del Inspector
    [SerializeField, HideInInspector] private bool _lootImported; // los spawns de antes del loot traen sus drops del enemigo una vez

    public GameObject Prefab => _prefab;
    public Variant[] Variants => _variants;
    public int Seed => _seed;
    public GameObject Instance => _instance;
    public GameObject[] Loot => _loot;
    public bool LootImported => _lootImported;

    private void Reset()
    {
        _seed = UnityEngine.Random.Range(1, int.MaxValue);
        _lootImported = true; // un spawn nuevo no tiene nada que traer
    }
}
