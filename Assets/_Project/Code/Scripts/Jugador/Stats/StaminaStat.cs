using UnityEngine;

public class StaminaStat : CharacterStat
{
    [SerializeField] private float sprintCostPerSecond = 20f;
    private bool _isSprinting = false;
    private bool _isExhausted = false;

    protected override void Update()
    {
        if (_isSprinting && !_isExhausted)
        {
            Consume(sprintCostPerSecond * Time.deltaTime);
        }
        else
        {
            base.Update(); // Regeneración normal
        }
    }

    public void StartSprinting()
    {
        if (!_isExhausted && currentValue > 0)
        {
            _isSprinting = true;
        }
    }

    public void StopSprinting()
    {
        _isSprinting = false;
    }

    public bool CanSprint()
    {
        return !_isExhausted && currentValue > 0;
    }

    protected override void Awake()
    {
        base.Awake();

        // Suscripción a eventos
        OnValueDepleted += (value) => {
            _isExhausted = true;
            _isSprinting = false;
        };

        OnValueFull += (value) => {
            _isExhausted = false;
        };
    }
}