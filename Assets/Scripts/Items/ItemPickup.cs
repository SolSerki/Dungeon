using UnityEngine;

// An ItemData lying on the floor: picking it up applies the item's effect.
public class ItemPickup : Pickup
{
    [SerializeField] private ItemData _item;
    [SerializeField] private AudioSource _audio;            // optional: only needed if the item has drop sounds

    public ItemData Item => _item;

    // Called by LootDropper right after spawning it (hand-placed items stay silent)
    public void PlayDropSound()
    {
        if (_item != null) Sfx.PlayRandom(_audio, _item.DropSounds, _item.DropVolume);
    }

    protected override bool Collect(Character character) => _item != null && _item.Apply(character);
}
