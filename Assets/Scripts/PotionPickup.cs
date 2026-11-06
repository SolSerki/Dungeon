using UnityEngine;

public class PotionPickup : Pickup
{
    [SerializeField] private float _healAmount = 2; // 2 HP = un corazón

    protected override bool Collect(Character character)
    {
        var health = character.GetComponent<HealthSystem>();
        if (health.CurrentHealth >= health.MaxHealth) return false; // con vida llena, se queda en el suelo

        health.Heal(_healAmount);
        return true;
    }
}