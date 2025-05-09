using UnityEngine;

public class HealthStat : CharacterStat
{
    public bool IsAlive => currentValue > 0;

    public void TakeDamage(float damage)
    {
        Consume(damage);
    }

    public void Heal(float amount)
    {
        Regenerate(amount);
    }
}