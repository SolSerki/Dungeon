using UnityEngine;

// An item that can lie on the floor and be picked up (Key, Potion...).
// The asset holds the data and the effect; ItemPickup is just "this item, lying here".
public abstract class ItemData : ScriptableObject
{
    [SerializeField] private ItemPickup _worldPrefab;      // what gets spawned on the floor when it drops

    [Header("Drop")]
    [SerializeField] private AudioClip[] _dropSounds;      // plays when an enemy drops it
    [SerializeField, Range(0f, 1f)] private float _dropVolume = 0.6f;

    public ItemPickup WorldPrefab => _worldPrefab;
    public AudioClip[] DropSounds => _dropSounds;
    public float DropVolume => _dropVolume;

    // Returns false if it can't be picked up right now (it stays on the ground)
    public abstract bool Apply(Character character);
}
