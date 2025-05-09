using UnityEngine;

public class PlayerStatsManager : MonoBehaviour
{
    // Referencias a los componentes de estad�sticas
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

        // Configurar eventos o inicializaci�n adicional
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
        // Aumentar estad�sticas base al subir de nivel
        Health.ModifyValue(Health.GetMaxValue() * 0.1f); // 10% m�s de salud
        Mana.ModifyValue(Mana.GetMaxValue() * 0.1f); // 10% m�s de mana
        Stamina.ModifyValue(Stamina.GetMaxValue() * 0.05f); //5% m�s de estamina

        // Otras recompensas de nivel
        Debug.Log($"�Subiste al nivel {newLevel}!");
    }
}