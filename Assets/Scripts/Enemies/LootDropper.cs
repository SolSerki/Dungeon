using System;
using UnityEngine;

[RequireComponent(typeof(HealthSystem))]
public class LootDropper : MonoBehaviour
{
    [Serializable]
    private struct Drop
    {
        public ItemData Item;
        [Range(0f, 1f)] public float Chance;
    }

    [SerializeField] private Drop[] _drops;
    [SerializeField] private float _scatterRadius = 0.3f;

    private HealthSystem _health;

    private void Awake() => _health = GetComponent<HealthSystem>();
    private void OnEnable() => _health.OnDied += DropLoot;
    private void OnDisable() => _health.OnDied -= DropLoot;

    private void DropLoot()
    {
        foreach (var drop in _drops)
        {
            if (drop.Item == null || drop.Item.WorldPrefab == null) continue;
            if (UnityEngine.Random.value > drop.Chance) continue;

            Vector2 offset = UnityEngine.Random.insideUnitCircle * _scatterRadius;
            var pickup = Instantiate(drop.Item.WorldPrefab, (Vector2)transform.position + offset, Quaternion.identity);
            pickup.PlayDropSound();
        }
    }
}
