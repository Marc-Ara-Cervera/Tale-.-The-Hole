using UnityEngine;

public class PlayerStatsManager : MonoBehaviour
{
    // Referencias a los componentes de estadísticas
    [HideInInspector] public HealthStat Health { get; private set; }
    [HideInInspector] public ManaStat Mana { get; private set; }
    [HideInInspector] public StaminaStat Stamina { get; private set; }
    [HideInInspector] public ExperienceStat Experience { get; private set; }

    private void Awake()
    {
        // Obtener o agregar componentes
        Health = GetOrAddComponent<HealthStat>();
        Mana = GetOrAddComponent<ManaStat>();
        Stamina = GetOrAddComponent<StaminaStat>();
        Experience = GetOrAddComponent<ExperienceStat>();

        // Configurar eventos o inicialización adicional
        Experience.OnLevelUp += HandleLevelUp;
    }

    private T GetOrAddComponent<T>() where T : Component
    {
        T component = GetComponent<T>();
        if (component == null)
        {
            component = gameObject.AddComponent<T>();
        }
        return component;
    }

    private void HandleLevelUp(int newLevel)
    {
        // Aumentar estadísticas base al subir de nivel
        Health.ModifyValue(Health.GetMaxValue() * 0.1f); // 10% más de salud
        Mana.ModifyValue(Mana.GetMaxValue() * 0.1f); // 10% más de mana
        Stamina.ModifyValue(Stamina.GetMaxValue() * 0.05f); //5% más de estamina

        // Otras recompensas de nivel
        Debug.Log($"¡Subiste al nivel {newLevel}!");
    }
}