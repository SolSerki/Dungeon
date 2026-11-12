using System;
using UnityEngine;

// Level Builder marker: says which enemy goes here.
// Its children are the patrol waypoints, in order.
public class EnemySpawn : MonoBehaviour
{
    [Serializable]
    public struct Variant
    {
        public GameObject Prefab;
        [Min(0f)] public float Weight;
    }

    [SerializeField] private GameObject _prefab;
    [Tooltip("Optional: with variants, the Level Builder picks one by weight instead of using Prefab")]
    [SerializeField] private Variant[] _variants = new Variant[0];
    [SerializeField, HideInInspector] private int _seed;          // fixes the roll so it doesn't change on every Build
    [SerializeField, HideInInspector] private GameObject _instance; // set by the Level Builder
    [SerializeField, HideInInspector] private ItemData[] _loot = new ItemData[0]; // edited with the Inspector toggles
    [SerializeField, HideInInspector] private bool _lootImported; // spawns from before loot existed pull their drops from the enemy once

    public GameObject Prefab => _prefab;
    public Variant[] Variants => _variants;
    public int Seed => _seed;
    public GameObject Instance => _instance;
    public ItemData[] Loot => _loot;
    public bool LootImported => _lootImported;

    private void Reset()
    {
        _seed = UnityEngine.Random.Range(1, int.MaxValue);
        _lootImported = true; // a new spawn has nothing to pull in
    }
}
