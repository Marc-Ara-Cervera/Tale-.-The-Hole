using UnityEngine;

public class HealthStat : CharacterStat, iDa�able
{
    public bool IsAlive => currentValue > 0;

    public void TakeDamage(float damage, GameObject instigator = null)
    {
        Consume(damage);
    }

    public void Heal(float amount)
    {
        Regenerate(amount);
    }
}