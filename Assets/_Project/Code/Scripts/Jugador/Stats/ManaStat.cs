using UnityEngine;

public class ManaStat : CharacterStat
{
    public bool CanCastSpell(float manaCost)
    {
        return HasEnoughValue(manaCost);
    }

    public void CastSpell(float manaCost)
    {
        Consume(manaCost);
    }
}