using System;
using UnityEngine;

public class ExperienceStat : CharacterStat
{
    [SerializeField] private int currentLevel = 1;
    [SerializeField] private float experienceToNextLevel = 100f;
    [SerializeField] private float experienceLevelMultiplier = 1.5f;

    public event Action<int> OnLevelUp;

    protected override void Awake()
    {
        // No inicializar currentValue a maxValue
        currentValue = 0f;
        maxValue = experienceToNextLevel;
        autoRegenerate = false;
    }

    public override void ModifyValue(float amount)
    {
        float oldValue = currentValue;
        currentValue += amount;

        // Verificar level up
        while (currentValue >= maxValue)
        {
            // Level up
            currentValue -= maxValue;
            currentLevel++;

            // Calcular nueva experiencia requerida
            maxValue = maxValue * experienceLevelMultiplier;

            // Notificar
            OnLevelUp?.Invoke(currentLevel);
        }

        RaiseValueChangedEvent(currentValue, maxValue);
    }

    public void AddExperience(float amount)
    {
        ModifyValue(amount);
    }

    public int GetLevel() => currentLevel;
}