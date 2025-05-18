using UnityEngine;

/// <summary>
/// Clase base abstracta para todos los hechizos del juego.
/// Se implementa como ScriptableObject para permitir crear diferentes hechizos desde el editor.
/// </summary>
public abstract class SpellBase : ScriptableObject
{
    #region Propiedades Básicas

    [Header("Información Básica")]
    [Tooltip("Nombre del hechizo")]
    [SerializeField] private string spellName;

    [Tooltip("Icono para mostrar en la UI")]
    [SerializeField] private Sprite icon;

    [Tooltip("Descripción del hechizo")]
    [TextArea(3, 5)]
    [SerializeField] private string description;

    [Header("Estadísticas del Hechizo")]
    [Tooltip("Cantidad de mana necesaria para lanzar el hechizo")]
    [SerializeField] private float manaCost = 10f;

    [Tooltip("Tiempo de enfriamiento en segundos")]
    [SerializeField] private float cooldownTime = 2f;

    [Tooltip("Tiempo mínimo de carga necesario para lanzar el hechizo (segundos)")]
    [SerializeField] private float minChargeTime = 0.5f;

    [Header("Efectos Visuales")]
    [Tooltip("Prefab a instanciar cuando se lanza el hechizo (opcional)")]
    [SerializeField] protected GameObject spellPrefab;

    [Tooltip("Efecto de sonido al lanzar el hechizo (opcional)")]
    [SerializeField] protected AudioClip castSound;

    [Tooltip("Prefab de partículas para la carga del hechizo (opcional)")]
    [SerializeField] protected GameObject chargingEffectPrefab;

    [Tooltip("Prefab de partículas para cuando se cancela la carga (opcional)")]
    [SerializeField] protected GameObject cancelEffectPrefab;
    private GameObject activeChargingEffect = null;

    [System.Serializable]
    public class MagicCircleConfig
    {
        [Tooltip("Prefab del círculo mágico")]
        public GameObject circlePrefab;

        [Tooltip("Desplazamiento de posición")]
        public Vector3 positionOffset = Vector3.zero;

        [Tooltip("Retraso de aparición (segundos)")]
        [Range(0f, 2f)] public float appearDelay = 0f;
    }

    [Header("Círculos Mágicos")]
    [Tooltip("Configuraciones de círculos mágicos para este hechizo")]
    [SerializeField] private MagicCircleConfig[] magicCircles = new MagicCircleConfig[0];

    // Tiempo del último lanzamiento (para gestionar cooldown)
    [System.NonSerialized] private float lastCastTime = -999f; // Inicializado a un valor negativo para permitir lanzar inmediatamente

    #endregion

    #region Getters Públicos

    /// <summary>
    /// Nombre del hechizo
    /// </summary>
    public string SpellName => spellName;

    /// <summary>
    /// Icono para UI
    /// </summary>
    public Sprite Icon => icon;

    /// <summary>
    /// Descripción del hechizo
    /// </summary>
    public string Description => description;

    /// <summary>
    /// Coste de mana
    /// </summary>
    public float ManaCost => manaCost;

    /// <summary>
    /// Tiempo de cooldown
    /// </summary>
    public float CooldownTime => cooldownTime;

    /// <summary>
    /// Tiempo restante de cooldown en segundos
    /// </summary>
    public float RemainingCooldown => Mathf.Max(0, (lastCastTime + cooldownTime) - Time.time);

    /// <summary>
    /// Porcentaje de cooldown completado (0-1)
    /// </summary>
    public float CooldownProgress => Mathf.Clamp01(RemainingCooldown / cooldownTime);

    /// <summary>
    /// Indica si el hechizo está listo para ser lanzado
    /// </summary>
    public bool IsReady()
    {
        float remainingTime = (lastCastTime + cooldownTime) - Time.time;
        bool ready = Time.time >= lastCastTime + cooldownTime;

        Debug.Log($"Spell: {name} | lastCastTime: {lastCastTime} | Time.time: {Time.time} | cooldownTime: {cooldownTime} | Remaining: {remainingTime} | IsReady: {ready}");

        return ready;
    }

    /// <summary>
    /// Método para saber si este hechizo usa círculos mágicos
    /// </summary>
    public bool HasMagicCircles()
    {
        return magicCircles != null && magicCircles.Length > 0;
    }

    /// <summary>
    /// Método para obtener las configuraciones de círculos
    /// </summary>
    public MagicCircleConfig[] GetMagicCircles()
    {
        return magicCircles;
    }

    /// <summary>
    /// Tiempo mínimo de carga necesario para lanzar el hechizo
    /// </summary>
    public float MinChargeTime => minChargeTime;

    /// <summary>
    /// Devuelve si el hechizo tiene un efecto de carga personalizado
    /// </summary>
    public bool HasCustomChargingEffect => chargingEffectPrefab != null;

    /// <summary>
    /// Devuelve si el hechizo tiene un efecto de cancelación personalizado
    /// </summary>
    public bool HasCustomCancelEffect => cancelEffectPrefab != null;

    #endregion

    #region Métodos Principales

    private void OnEnable()
    {
        // Reiniciar el cooldown cuando el objeto se carga
        lastCastTime = -999f;
        Debug.Log($"Spell: {name} | Reiniciado en OnEnable. lastCastTime = {lastCastTime}");
    }

    /// <summary>
    /// Método principal para lanzar el hechizo
    /// </summary>
    /// <param name="origin">Punto de origen para el hechizo (posición del bastón)</param>
    /// <param name="caster">Referencia al lanzador (para acceder a sus estadísticas)</param>
    public virtual void Cast(Transform origin, PlayerStatsManager caster)
    {
        // Actualizar el tiempo del último lanzamiento
        float previousLastCastTime = lastCastTime;
        lastCastTime = Time.time;

        Debug.Log($"Spell: {name} | Cast called! Previous lastCastTime: {previousLastCastTime} | New lastCastTime: {lastCastTime}");

        // Reproducir sonido si está disponible
        if (castSound != null)
        {
            AudioSource.PlayClipAtPoint(castSound, origin.position);
        }

        // Ejecutar el efecto específico del hechizo (implementado por las clases hijas)
        ExecuteSpellEffect(origin, caster);
    }

    /// <summary>
    /// Método abstracto que cada hechizo debe implementar con su efecto específico
    /// </summary>
    protected abstract void ExecuteSpellEffect(Transform origin, PlayerStatsManager caster);

    /// <summary>
    /// Reinicia el cooldown del hechizo
    /// </summary>
    public virtual void ResetCooldown()
    {
        lastCastTime = -999f;
    }

    /// <summary>
    /// Modifica el cooldown para pruebas o efectos especiales
    /// </summary>
    public virtual void ModifyCooldown(float multiplier)
    {
        // Si el hechizo está en cooldown, ajustar el tiempo restante
        if (!IsReady())
        {
            float remainingTime = (lastCastTime + cooldownTime) - Time.time;
            float newRemainingTime = remainingTime * multiplier;
            lastCastTime = Time.time - cooldownTime + newRemainingTime;
        }
    }

    /// <summary>
    /// Crea el efecto visual de carga específico del hechizo
    /// </summary>
    /// <param name="attachPoint">Punto donde anclar el efecto</param>
    public GameObject CreateChargingEffect(Transform attachPoint)
    {
        if (chargingEffectPrefab == null)
            return null;

        // Crear el efecto de carga y guardarlo para poder destruirlo después
        activeChargingEffect = Instantiate(chargingEffectPrefab, attachPoint.position, attachPoint.rotation, attachPoint);
        return activeChargingEffect;
    }

    /// <summary>
    /// Detiene el efecto visual de carga
    /// </summary>
    public void StopChargingEffect()
    {
        if (activeChargingEffect != null)
        {
            Destroy(activeChargingEffect);
            activeChargingEffect = null;
        }
    }

    /// <summary>
    /// Crea el efecto visual de cancelación específico del hechizo
    /// </summary>
    /// <param name="position">Posición donde crear el efecto</param>
    /// <param name="rotation">Rotación del efecto</param>
    public void CreateCancelEffect(Vector3 position, Quaternion rotation)
    {
        if (cancelEffectPrefab != null)
        {
            GameObject cancelEffect = Instantiate(cancelEffectPrefab, position, rotation);

            // Destruir automáticamente después de un tiempo razonable
            ParticleSystem ps = cancelEffect.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                float duration = ps.main.duration + ps.main.startLifetime.constantMax;
                Destroy(cancelEffect, duration);
            }
            else
            {
                Destroy(cancelEffect, 2f); // Tiempo por defecto si no tiene ParticleSystem
            }
        }
    }

    #endregion
}