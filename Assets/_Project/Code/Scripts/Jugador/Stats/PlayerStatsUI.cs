using UnityEngine;
using UnityEngine.UI;

public class PlayerStatsUI : MonoBehaviour
{
    [Header("Referencias UI")]
    [SerializeField] private Slider healthSlider;
    [SerializeField] private Slider manaSlider;
    [SerializeField] private Slider staminaSlider;

    [Header("Configuración Animación")]
    [SerializeField] private float transitionSpeed = 5f; // Velocidad de la animación

    // Valores actuales y valores objetivo para la animación
    private float healthTarget;
    private float manaTarget;
    private float staminaTarget;

    private PlayerStatsManager statsManager;

    private void Start()
    {
        statsManager = FindObjectOfType<PlayerStatsManager>();
        if (statsManager == null)
        {
            Debug.LogError("No se encontró PlayerStatsManager para UI");
            return;
        }

        // Inicializar los valores objetivo con valores actuales
        healthTarget = statsManager.Health.GetNormalizedValue();
        manaTarget = statsManager.Mana.GetNormalizedValue();
        staminaTarget = statsManager.Stamina.GetNormalizedValue();

        // Configuración inicial de los sliders
        SetupSlider(healthSlider, statsManager.Health);
        SetupSlider(manaSlider, statsManager.Mana);
        SetupSlider(staminaSlider, statsManager.Stamina);

        // Suscribirse a eventos
        statsManager.Health.OnValueChanged += UpdateHealthTargetValue;
        statsManager.Mana.OnValueChanged += UpdateManaTargetValue;
        statsManager.Stamina.OnValueChanged += UpdateStaminaTargetValue;
    }

    private void SetupSlider(Slider slider, CharacterStat stat)
    {
        if (slider != null)
        {
            slider.minValue = -0.087f;
            slider.maxValue = 1.083f; // Trabajamos con valores normalizados
            slider.value = stat.GetNormalizedValue();
        }
    }

    private void Update()
    {
        // Actualizar suavemente los sliders en cada frame
        AnimateSlider(healthSlider, healthTarget);
        AnimateSlider(manaSlider, manaTarget);
        AnimateSlider(staminaSlider, staminaTarget);
    }

    private void AnimateSlider(Slider slider, float targetValue)
    {
        if (slider != null)
        {
            // Interpolar suavemente hacia el valor objetivo
            slider.value = Mathf.Lerp(slider.value, targetValue, Time.deltaTime * transitionSpeed);
        }
    }

    // Estos métodos se llaman cuando cambian los valores de las estadísticas
    private void UpdateHealthTargetValue(float current, float max)
    {
        healthTarget = current / max;
    }

    private void UpdateManaTargetValue(float current, float max)
    {
        manaTarget = current / max;
    }

    private void UpdateStaminaTargetValue(float current, float max)
    {
        staminaTarget = current / max;
    }
}