using System;
using UnityEngine;

public abstract class CharacterStat : MonoBehaviour
{
    [SerializeField] protected float maxValue = 100f;
    [SerializeField] protected float currentValue;
    [SerializeField] protected float regenerationRate = 10f;
    [SerializeField] protected bool autoRegenerate = true;

    // Eventos para notificar cambios
    public event Action<float, float> OnValueChanged; // (current, max)
    public event Action<float> OnValueDepleted;
    public event Action<float> OnValueFull;

    protected virtual void Awake()
    {
        currentValue = maxValue;
    }

    protected virtual void Update()
    {
        if (autoRegenerate && currentValue < maxValue)
        {
            Regenerate(regenerationRate * Time.deltaTime);
        }
    }

    public virtual void ModifyValue(float amount)
    {
        float oldValue = currentValue;
        currentValue = Mathf.Clamp(currentValue + amount, 0f, maxValue);

        // Invoca eventos según sea necesario
        if (oldValue != currentValue)
        {
            OnValueChanged?.Invoke(currentValue, maxValue);

            if (currentValue <= 0f && oldValue > 0f)
                OnValueDepleted?.Invoke(0f);

            if (currentValue >= maxValue && oldValue < maxValue)
                OnValueFull?.Invoke(maxValue);
        }
    }

    public virtual void Regenerate(float amount)
    {
        ModifyValue(amount);
    }

    public virtual void Consume(float amount)
    {
        ModifyValue(-amount);
    }

    public bool HasEnoughValue(float amount)
    {
        return currentValue >= amount;
    }

    protected virtual void RaiseValueChangedEvent(float current, float max)
    {
        OnValueChanged?.Invoke(current, max);
    }

    public float GetCurrentValue() => currentValue;
    public float GetMaxValue() => maxValue;
    public float GetNormalizedValue() => currentValue / maxValue;
}