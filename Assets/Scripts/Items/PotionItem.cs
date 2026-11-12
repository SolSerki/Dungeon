using UnityEngine;

[CreateAssetMenu(menuName = "Dungeon/Items/Potion", fileName = "Potion")]
public class PotionItem : ItemData
{
    [SerializeField] private float _healAmount = 2; // 2 HP = one heart

    public override bool Apply(Character character)
    {
        var health = character.GetComponent<HealthSystem>();
        if (health == null || health.CurrentHealth >= health.MaxHealth) return false; // at full health it stays on the ground

        health.Heal(_healAmount);
        return true;
    }
}
