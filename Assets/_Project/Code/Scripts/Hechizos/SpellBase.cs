using UnityEngine;

/// <summary>
/// Estructura que contiene toda la información contextual necesaria para lanzar un hechizo
/// Ahora incluye información sobre comandos y targeting
/// </summary>
[System.Serializable]
public struct SpellCastContext
{
    public Transform staffTransform;        // Transform del bastón
    public Transform playerTransform;       // Transform del jugador
    public Vector3 targetPosition;          // Posición objetivo (del raycast)
    public Vector3 targetNormal;            // Normal de la superficie objetivo
    public bool hasValidTarget;             // Si hay un objetivo válido
    public PlayerStatsManager caster;       // Referencia al lanzador

    // NUEVO: Información de comando
    public SpellCommandType commandUsed;    // Qué comando se usó para ejecutar
    public Vector3 commandDirection;        // Dirección del comando (para gestos direccionales)
    public float commandIntensity;          // Intensidad del comando (velocidad del gesto, etc.)

    public SpellCastContext(Transform staff, Transform player, PlayerStatsManager statsManager)
    {
        staffTransform = staff;
        playerTransform = player;
        targetPosition = Vector3.zero;
        targetNormal = Vector3.up;
        hasValidTarget = false;
        caster = statsManager;
        commandUsed = SpellCommandType.INSTANT;
        commandDirection = Vector3.forward;
        commandIntensity = 1f;
    }
}

/// <summary>
/// Clase base abstracta para todos los hechizos del juego.
/// Ahora incluye sistema de comandos post-lanzamiento
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

    [Header("Configuración de Origen")]
    [Tooltip("Configuración de dónde se origina este hechizo")]
    [SerializeField] private SpellOriginConfig originConfig = new SpellOriginConfig();

    [Header("Sistema de Comandos - NUEVO")]
    [Tooltip("Tipo de comando requerido después de la preparación")]
    [SerializeField] private SpellCommandType commandType = SpellCommandType.INSTANT;

    [Tooltip("Prefab que aparece durante la fase de preparación (versión fantasma del hechizo)")]
    [SerializeField] private GameObject preparationPrefab;

    [Tooltip("Tiempo máximo para dar el comando (segundos). 0 = sin límite")]
    [Range(0f, 10f)]
    [SerializeField] private float commandTimeout = 4f;

    [Tooltip("¿Se puede ejecutar inmediatamente presionando el gatillo de nuevo?")]
    [SerializeField] private bool allowInstantFallback = true;

    [Header("Configuración Específica por Comando")]
    [Tooltip("Tiempo que debe mantener la puntería para comandos DIRECTIONAL (segundos)")]
    [Range(0.5f, 5f)]
    [SerializeField] private float aimHoldTime = 2f;

    [Tooltip("Tolerancia de movimiento para detectar 'mantener quieto' (unidades)")]
    [Range(0.01f, 0.5f)]
    [SerializeField] private float aimTolerance = 0.1f;

    [Tooltip("Velocidad mínima requerida para gestos EMERGE/DESCEND")]
    [Range(0.5f, 5f)]
    [SerializeField] private float minimumGestureSpeed = 1.5f;

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
    [System.NonSerialized] private float lastCastTime = -999f;

    #endregion

    #region Getters Públicos

    /// <summary>
    /// Configuración de origen del hechizo
    /// </summary>
    public SpellOriginConfig OriginConfig => originConfig;

    /// <summary>
    /// Tipo de comando requerido
    /// </summary>
    public SpellCommandType CommandType => commandType;

    /// <summary>
    /// Prefab de preparación (versión fantasma)
    /// </summary>
    public GameObject PreparationPrefab => preparationPrefab;

    /// <summary>
    /// Tiempo límite para comandos
    /// </summary>
    public float CommandTimeout => commandTimeout;

    /// <summary>
    /// ¿Permite ejecución instantánea como fallback?
    /// </summary>
    public bool AllowInstantFallback => allowInstantFallback;

    /// <summary>
    /// Tiempo requerido para mantener la puntería
    /// </summary>
    public float AimHoldTime => aimHoldTime;

    /// <summary>
    /// Tolerancia de movimiento para puntería
    /// </summary>
    public float AimTolerance => aimTolerance;

    /// <summary>
    /// Velocidad mínima para gestos
    /// </summary>
    public float MinimumGestureSpeed => minimumGestureSpeed;

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

    /// <summary>
    /// Indica si el hechizo requiere preparación (no es instantáneo)
    /// </summary>
    public bool RequiresPreparation => commandType != SpellCommandType.INSTANT;

    /// <summary>
    /// Indica si tiene prefab de preparación configurado
    /// </summary>
    public bool HasPreparationPrefab => preparationPrefab != null;

    #endregion

    #region Métodos Principales

    private void OnEnable()
    {
        // Reiniciar el cooldown cuando el objeto se carga
        lastCastTime = -999f;
        Debug.Log($"Spell: {name} | Reiniciado en OnEnable. lastCastTime = {lastCastTime}");
    }

    /// <summary>
    /// Método principal para lanzar el hechizo con el nuevo sistema de contexto
    /// NUEVO: Ahora maneja tanto preparación como ejecución
    /// </summary>
    /// <param name="context">Contexto completo para el lanzamiento del hechizo</param>
    public virtual void Cast(SpellCastContext context)
    {
        // Calcular la posición y rotación de origen según la configuración
        OriginData originData = CalculateOriginData(context);

        // Actualizar el tiempo del último lanzamiento
        float previousLastCastTime = lastCastTime;
        lastCastTime = Time.time;

        Debug.Log($"Spell: {name} | Cast called! Previous lastCastTime: {previousLastCastTime} | New lastCastTime: {lastCastTime}");
        Debug.Log($"Spell: {name} | Origin Type: {originConfig.originType} | Command Type: {commandType} | Position: {originData.position}");

        // Reproducir sonido si está disponible
        if (castSound != null)
        {
            AudioSource.PlayClipAtPoint(castSound, originData.position);
        }

        // Ejecutar el efecto específico del hechizo (implementado por las clases hijas)
        ExecuteSpellEffect(originData, context);
    }

    /// <summary>
    /// NUEVO: Método para crear el efecto de preparación
    /// Se llama cuando el hechizo está listo pero esperando comando
    /// </summary>
    /// <param name="context">Contexto del hechizo</param>
    /// <returns>GameObject del efecto de preparación creado</returns>
    public virtual GameObject CreatePreparationEffect(SpellCastContext context)
    {
        if (preparationPrefab == null)
            return null;

        // Calcular dónde aparece el efecto de preparación
        OriginData originData = CalculateOriginData(context);

        // Instanciar el prefab de preparación
        GameObject preparationEffect = Instantiate(preparationPrefab, originData.position, originData.rotation);

        Debug.Log($"Spell: {name} | Preparation effect created at {originData.position}");

        return preparationEffect;
    }

    /// <summary>
    /// Estructura que contiene la información calculada del origen
    /// </summary>
    public struct OriginData
    {
        public Vector3 position;
        public Quaternion rotation;
        public Transform referenceTransform;

        public OriginData(Vector3 pos, Quaternion rot, Transform reference = null)
        {
            position = pos;
            rotation = rot;
            referenceTransform = reference;
        }
    }

    /// <summary>
    /// Calcula la posición y rotación de origen según la configuración del hechizo
    /// </summary>
    private OriginData CalculateOriginData(SpellCastContext context)
    {
        Vector3 basePosition = Vector3.zero;
        Quaternion baseRotation = Quaternion.identity;
        Transform referenceTransform = null;

        switch (originConfig.originType)
        {
            case SpellOriginType.STAFF_TIP:
                basePosition = context.staffTransform.position;
                baseRotation = context.staffTransform.rotation;
                referenceTransform = context.staffTransform;
                break;

            case SpellOriginType.PLAYER_CENTER:
                basePosition = context.playerTransform.position + Vector3.up * 1.2f; // Altura del pecho
                baseRotation = context.playerTransform.rotation;
                referenceTransform = context.playerTransform;
                break;

            case SpellOriginType.PLAYER_FEET:
                basePosition = context.playerTransform.position;
                baseRotation = context.playerTransform.rotation;
                referenceTransform = context.playerTransform;
                break;

            case SpellOriginType.PLAYER_FRONT:
                basePosition = context.playerTransform.position +
                              context.playerTransform.forward * originConfig.DistanceInFrontOfPlayer +
                              Vector3.up * 1.2f; // Altura del pecho
                baseRotation = context.playerTransform.rotation;
                referenceTransform = context.playerTransform;
                break;

            case SpellOriginType.TARGET_POINT:
                if (context.hasValidTarget)
                {
                    basePosition = context.targetPosition;
                    baseRotation = Quaternion.LookRotation(context.targetNormal);
                }
                else
                {
                    // Fallback al bastón si no hay objetivo
                    basePosition = context.staffTransform.position;
                    baseRotation = context.staffTransform.rotation;
                }
                break;

            case SpellOriginType.TARGET_ABOVE:
                if (context.hasValidTarget)
                {
                    basePosition = context.targetPosition + Vector3.up * originConfig.HeightAboveTarget;
                    baseRotation = Quaternion.LookRotation(Vector3.down);
                }
                else
                {
                    // Fallback al bastón si no hay objetivo
                    basePosition = context.staffTransform.position + Vector3.up * originConfig.HeightAboveTarget;
                    baseRotation = Quaternion.LookRotation(Vector3.down);
                }
                break;

            case SpellOriginType.TARGET_SURFACE:
                if (context.hasValidTarget)
                {
                    basePosition = context.targetPosition;
                    baseRotation = Quaternion.LookRotation(context.targetNormal);
                }
                else
                {
                    // Fallback al suelo bajo el jugador
                    basePosition = context.playerTransform.position;
                    baseRotation = context.playerTransform.rotation;
                }
                break;

            case SpellOriginType.WORLD_FIXED:
                basePosition = originConfig.WorldFixedPosition;
                baseRotation = Quaternion.identity;
                break;
        }

        // Aplicar offsets configurados
        Vector3 finalPosition = basePosition + baseRotation * originConfig.positionOffset;
        Quaternion finalRotation = baseRotation * Quaternion.Euler(originConfig.rotationOffset);

        return new OriginData(finalPosition, finalRotation, referenceTransform);
    }

    /// <summary>
    /// Método abstracto que cada hechizo debe implementar con su efecto específico
    /// Ahora recibe OriginData calculado y el contexto completo
    /// </summary>
    protected abstract void ExecuteSpellEffect(OriginData origin, SpellCastContext context);

    /// <summary>
    /// Método de compatibilidad con el sistema anterior (DEPRECATED)
    /// </summary>
    [System.Obsolete("Use Cast(SpellCastContext) instead")]
    public virtual void Cast(Transform origin, PlayerStatsManager caster)
    {
        // Crear contexto básico para compatibilidad
        SpellCastContext context = new SpellCastContext(origin, caster.transform, caster);
        Cast(context);
    }

    /// <summary>
    /// Método de compatibilidad para ExecuteSpellEffect (DEPRECATED)
    /// </summary>
    [System.Obsolete("Use ExecuteSpellEffect(OriginData, SpellCastContext) instead")]
    protected virtual void ExecuteSpellEffect(Transform origin, PlayerStatsManager caster)
    {
        // Método vacío para compatibilidad - las clases hijas deben usar el nuevo método
    }

    #region Métodos de Cooldown (sin cambios)

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
        if (!IsReady())
        {
            float remainingTime = (lastCastTime + cooldownTime) - Time.time;
            float newRemainingTime = remainingTime * multiplier;
            lastCastTime = Time.time - cooldownTime + newRemainingTime;
        }
    }

    #endregion

    #region Métodos de Efectos Visuales (sin cambios)

    /// <summary>
    /// Crea el efecto visual de carga específico del hechizo
    /// </summary>
    public GameObject CreateChargingEffect(Transform attachPoint)
    {
        if (chargingEffectPrefab == null)
            return null;

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
    public void CreateCancelEffect(Vector3 position, Quaternion rotation)
    {
        if (cancelEffectPrefab != null)
        {
            GameObject cancelEffect = Instantiate(cancelEffectPrefab, position, rotation);

            ParticleSystem ps = cancelEffect.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                float duration = ps.main.duration + ps.main.startLifetime.constantMax;
                Destroy(cancelEffect, duration);
            }
            else
            {
                Destroy(cancelEffect, 2f);
            }
        }
    }

    #endregion

    #endregion
}