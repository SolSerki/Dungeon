using UnityEngine;

public class KeyPickup :  Pickup
{
    protected override bool Collect(Character character)
    {
        var inventory = character.GetComponent<Inventory>();
        if (inventory == null) return false;

        inventory.AddKey();
        return true;
    }

}
