using UnityEngine;

[CreateAssetMenu(menuName = "Dungeon/Items/Key", fileName = "Key")]
public class KeyItem : ItemData
{
    public override bool Apply(Character character)
    {
        var inventory = character.GetComponent<Inventory>();
        if (inventory == null) return false;

        inventory.AddKey();
        return true;
    }
}
