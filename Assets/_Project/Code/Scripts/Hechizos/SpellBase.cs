using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;

#region Enums y Estructuras

/// <summary>
/// Define los diferentes tipos de origen desde donde pueden aparecer los hechizos
/// </summary>
public enum SpellOriginType
{
    [System.ComponentModel.Description("Desde la punta del bastón")]
    STAFF_TIP,
    [System.ComponentModel.Description("Desde el centro del jugador (altura del pecho)")]
    PLAYER_CENTER,
    [System.ComponentModel.Description("Desde los pies del jugador")]
    PLAYER_FEET,
    [System.ComponentModel.Description("2-3 metros delante del jugador")]
    PLAYER_FRONT,
    [System.ComponentModel.Description("En el punto exacto donde apunta el bastón")]
    TARGET_POINT,
    [System.ComponentModel.Description("X metros encima del punto objetivo")]
    TARGET_ABOVE,
    [System.ComponentModel.Description("En la superficie del objetivo")]
    TARGET_SURFACE,
    [System.ComponentModel.Description("Punto fijo en el mundo (coordenadas específicas)")]
    WORLD_FIXED
}

/// <summary>
/// Define los tipos de comandos post-lanzamiento
/// </summary>
public enum SpellCommandType
{
    [System.ComponentModel.Description("Apuntar hacia el objetivo durante unos segundos")]
    DIRECTIONAL,
    [System.ComponentModel.Description("Gesto de bastón hacia arriba (invocar desde el suelo)")]
    EMERGE,
    [System.ComponentModel.Description("Gesto de bastón hacia abajo (invocar desde el cielo)")]
    DESCEND,
    [System.ComponentModel.Description("Sin comando adicional, se ejecuta inmediatamente")]
    INSTANT
}

/// <summary>
/// Define las propiedades escalables de los hechizos
/// </summary>
public enum ScalableProperty
{
    [System.ComponentModel.Description("Sin propiedad escalable")]
    NONE,
    [System.ComponentModel.Description("Cantidad de daño causado")]
    DAMAGE,
    [System.ComponentModel.Description("Cantidad de curación proporcionada")]
    HEALING,
    [System.ComponentModel.Description("Resistencia del escudo o barrera")]
    SHIELD_STRENGTH,
    [System.ComponentModel.Description("Potencia de efectos de buff/debuff")]
    BOOST,
    [System.ComponentModel.Description("Fuerza de empuje o knockback")]
    KNOCKBACK,
    [System.ComponentModel.Description("Duración de efectos temporales")]
    DURATION,
    [System.ComponentModel.Description("Velocidad de proyectiles")]
    PROJECTILE_SPEED,
    [System.ComponentModel.Description("Rango o alcance del hechizo")]
    RANGE
}

/// <summary>
/// Configuración del origen de un hechizo
/// </summary>
[System.Serializable]
public class SpellOriginConfig
{
    [Header("Tipo de Origen")]
    [Tooltip("Desde dónde se origina el hechizo")]
    public SpellOriginType originType = SpellOriginType.STAFF_TIP;

    [Header("Ajustes de Posición")]
    [Tooltip("Desplazamiento adicional en coordenadas locales")]
    public Vector3 positionOffset = Vector3.zero;

    [Tooltip("Rotación adicional en grados")]
    public Vector3 rotationOffset = Vector3.zero;

    [Header("Parámetros Específicos")]
    [Tooltip("Altura sobre el objetivo (solo TARGET_ABOVE)")]
    public float heightAboveTarget = 10f;

    [Tooltip("Distancia delante del jugador (solo PLAYER_FRONT)")]
    public float distanceInFrontOfPlayer = 2.5f;

    [Tooltip("Coordenadas fijas del mundo (solo WORLD_FIXED)")]
    public Vector3 worldFixedPosition = Vector3.zero;

    public SpellOriginConfig() { }

    public SpellOriginConfig(SpellOriginType type)
    {
        originType = type;
    }

    public SpellOriginConfig Clone()
    {
        return new SpellOriginConfig(originType)
        {
            positionOffset = positionOffset,
            rotationOffset = rotationOffset,
            heightAboveTarget = heightAboveTarget,
            distanceInFrontOfPlayer = distanceInFrontOfPlayer,
            worldFixedPosition = worldFixedPosition
        };
    }
}

/// <summary>
/// Contexto completo para lanzar un hechizo
/// </summary>
[System.Serializable]
public struct SpellCastContext
{
    public Transform staffTransform;
    public Transform playerTransform;
    public Vector3 targetPosition;
    public Vector3 targetNormal;
    public bool hasValidTarget;
    public PlayerStatsManager caster;

    // Información de comando
    public SpellCommandType commandUsed;
    public Vector3 commandDirection;
    public float commandIntensity;

    // Información de escalado
    public float spellScale;
    public bool wasScaled;
    public SpellScalingResult scalingResult;

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
        spellScale = 1f;
        wasScaled = false;
        scalingResult = new SpellScalingResult(1f);
    }

    public void UpdateScaling(float scale, SpellBase spell)
    {
        spellScale = scale;
        wasScaled = !Mathf.Approximately(scale, 1f);

        if (spell != null)
        {
            float availableMana = caster != null ? caster.Mana.GetCurrentValue() : float.MaxValue;
            scalingResult = spell.CalculateScaledProperties(scale, availableMana);
        }
    }

    public string GetScalingDescription()
    {
        if (!wasScaled) return "Tamaño normal";
        return spellScale > 1f ? $"Aumentado {spellScale:F1}x" : $"Reducido {spellScale:F1}x";
    }
}

/// <summary>
/// Resultado del escalado de un hechizo
/// </summary>
[System.Serializable]
public struct SpellScalingResult
{
    public float scale;
    public float primaryPropertyValue;
    public float secondaryPropertyValue;
    public float speedMultiplier;
    public float finalManaCost;
    public bool isViable;

    public SpellScalingResult(float scale)
    {
        this.scale = scale;
        primaryPropertyValue = 1f;
        secondaryPropertyValue = 1f;
        speedMultiplier = 1f;
        finalManaCost = 0f;
        isViable = true;
    }
}

/// <summary>
/// Datos del origen calculado
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

#endregion

/// <summary>
/// Clase base para todos los hechizos del juego
/// </summary>
public abstract class SpellBase : ScriptableObject
{
    #region Configuración del Inspector

    [Header("━━━━━ INFORMACIÓN BÁSICA ━━━━━")]
    [SerializeField, Tooltip("Nombre del hechizo")]
    private string spellName;

    [SerializeField, Tooltip("Icono para la interfaz")]
    private Sprite icon;

    [SerializeField, Tooltip("Descripción del hechizo"), TextArea(3, 5)]
    private string description;

    [Header("━━━━━ CONFIGURACIÓN DE ORIGEN ━━━━━")]
    [SerializeField, Tooltip("Configuración de dónde aparece el hechizo")]
    private SpellOriginConfig originConfig = new SpellOriginConfig();

    [Header("━━━━━ SISTEMA DE COMANDOS ━━━━━")]
    [SerializeField, Tooltip("Tipo de comando requerido tras la preparación")]
    private SpellCommandType commandType = SpellCommandType.INSTANT;

    [SerializeField, Tooltip("Prefab que aparece durante la preparación")]
    private GameObject preparationPrefab;

    [SerializeField, Tooltip("Tiempo máximo para ejecutar el comando (0 = sin límite)"), Range(0f, 10f)]
    private float commandTimeout = 4f;

    [SerializeField, Tooltip("¿Permite ejecución instantánea como alternativa?")]
    private bool allowInstantFallback = true;

    [Header("Parámetros por Comando")]
    [SerializeField, Tooltip("Tiempo de apuntado para DIRECTIONAL"), Range(0.5f, 5f)]
    private float aimHoldTime = 2f;

    [SerializeField, Tooltip("Tolerancia de movimiento al apuntar"), Range(0.01f, 0.5f)]
    private float aimTolerance = 0.1f;

    [SerializeField, Tooltip("Velocidad mínima para gestos"), Range(0.5f, 5f)]
    private float minimumGestureSpeed = 1.5f;

    [Header("━━━━━ ESTADÍSTICAS ━━━━━")]
    [SerializeField, Tooltip("Coste de maná")]
    private float manaCost = 10f;

    [SerializeField, Tooltip("Tiempo de reutilización (segundos)")]
    private float cooldownTime = 2f;

    [SerializeField, Tooltip("Tiempo mínimo de carga (segundos)")]
    private float minChargeTime = 0.5f;

    [Header("━━━━━ SISTEMA DE ESCALADO ━━━━━")]
    [SerializeField, Tooltip("¿Permite modificar el tamaño?")]
    private bool allowScaling = true;

    [SerializeField, Tooltip("Propiedad principal que se escala")]
    private ScalableProperty primaryProperty = ScalableProperty.DAMAGE;

    [SerializeField, Tooltip("Valor base de la propiedad principal")]
    private float primaryPropertyBaseValue = 25f;

    [SerializeField, Tooltip("Propiedad secundaria (opcional)")]
    private ScalableProperty secondaryProperty = ScalableProperty.NONE;

    [SerializeField, Tooltip("Valor base de la propiedad secundaria")]
    private float secondaryPropertyBaseValue = 0f;

    [Header("Límites de Escalado")]
    [SerializeField, Tooltip("Escala mínima permitida"), Range(0.1f, 1f)]
    private float minScale = 0.5f;

    [SerializeField, Tooltip("Escala máxima permitida"), Range(1f, 10f)]
    private float maxScale = 4.0f;

    [SerializeField, Tooltip("Umbral de eficiencia óptima"), Range(1f, 5f)]
    private float optimalScaleThreshold = 2.0f;

    [SerializeField, Tooltip("Suavizado del escalado"), Range(0.01f, 0.5f)]
    private float scalingSmoothingFactor = 0.1f;

    [Header("Curvas de Balance")]
    [SerializeField, Tooltip("Curva de la propiedad principal")]
    private AnimationCurve primaryPropertyCurve = AnimationCurve.EaseInOut(0.5f, 1.5f, 4f, 0.3f);

    [SerializeField, Tooltip("Curva de la propiedad secundaria")]
    private AnimationCurve secondaryPropertyCurve = AnimationCurve.Linear(0.5f, 1.2f, 4f, 0.7f);

    [SerializeField, Tooltip("Curva de velocidad")]
    private AnimationCurve speedCurve = AnimationCurve.EaseInOut(0.5f, 1.3f, 4f, 0.4f);

    [SerializeField, Tooltip("Curva de coste de maná")]
    private AnimationCurve manaCostCurve = AnimationCurve.EaseInOut(0.5f, 0.8f, 4f, 2.5f);

    [Header("━━━━━ EFECTOS VISUALES ━━━━━")]
    [SerializeField, Tooltip("Prefab principal del hechizo")]
    protected GameObject spellPrefab;

    [SerializeField, Tooltip("Sonido al lanzar")]
    protected AudioClip castSound;

    [SerializeField, Tooltip("Efecto de carga")]
    protected GameObject chargingEffectPrefab;

    [SerializeField, Tooltip("Efecto de cancelación")]
    protected GameObject cancelEffectPrefab;

    [System.Serializable]
    public class MagicCircleConfig
    {
        [Tooltip("Prefab del círculo mágico")]
        public GameObject circlePrefab;

        [Tooltip("Desplazamiento de posición")]
        public Vector3 positionOffset = Vector3.zero;

        [Tooltip("Retraso de aparición"), Range(0f, 2f)]
        public float appearDelay = 0f;
    }

    [Header("━━━━━ CÍRCULOS MÁGICOS ━━━━━")]
    [SerializeField, Tooltip("Configuración de círculos mágicos")]
    private MagicCircleConfig[] magicCircles = new MagicCircleConfig[0];

    #endregion

    #region Variables Privadas

    [System.NonSerialized] private float lastCastTime = -999f;
    [System.NonSerialized] private GameObject activeChargingEffect = null;

    // Cache de valores calculados
    private bool? _hasValidCurves;
    private Dictionary<float, SpellScalingResult> _scalingCache = new Dictionary<float, SpellScalingResult>();

    #endregion

    #region Propiedades Públicas

    // Información básica
    public string SpellName => spellName;
    public Sprite Icon => icon;
    public string Description => description;

    // Configuración
    public SpellOriginConfig OriginConfig => originConfig;
    public SpellCommandType CommandType => commandType;
    public GameObject PreparationPrefab => preparationPrefab;
    public float CommandTimeout => commandTimeout;
    public bool AllowInstantFallback => allowInstantFallback;

    // Parámetros de comando
    public float AimHoldTime => aimHoldTime;
    public float AimTolerance => aimTolerance;
    public float MinimumGestureSpeed => minimumGestureSpeed;

    // Estadísticas
    public float ManaCost => manaCost;
    public float CooldownTime => cooldownTime;
    public float MinChargeTime => minChargeTime;
    public float RemainingCooldown => Mathf.Max(0, (lastCastTime + cooldownTime) - Time.time);
    public float CooldownProgress => CooldownTime > 0 ? RemainingCooldown / CooldownTime : 0f;

    // Sistema de escalado
    public bool AllowScaling => allowScaling;
    public ScalableProperty PrimaryProperty => primaryProperty;
    public ScalableProperty SecondaryProperty => secondaryProperty;
    public float MinScale => minScale;
    public float MaxScale => maxScale;
    public float OptimalScaleThreshold => optimalScaleThreshold;
    public float ScalingSmoothingFactor => scalingSmoothingFactor;

    // Características del hechizo
    public bool HasCustomChargingEffect => chargingEffectPrefab != null;
    public bool HasCustomCancelEffect => cancelEffectPrefab != null;
    public bool RequiresPreparation => commandType != SpellCommandType.INSTANT;
    public bool HasPreparationPrefab => preparationPrefab != null;
    public bool HasMagicCircles() => magicCircles != null && magicCircles.Length > 0;
    public MagicCircleConfig[] GetMagicCircles() => magicCircles;

    #endregion

    #region Inicialización

    private void OnEnable()
    {
        lastCastTime = -999f;
        _scalingCache.Clear();
        ValidateCurves();
    }

    private void ValidateCurves()
    {
        _hasValidCurves = primaryPropertyCurve != null &&
                         speedCurve != null &&
                         manaCostCurve != null;
    }

    #endregion

    #region Métodos Públicos Principales

    public bool IsReady()
    {
        return Time.time >= lastCastTime + cooldownTime;
    }

    public virtual void Cast(SpellCastContext context)
    {
        var originData = CalculateOriginData(context);
        lastCastTime = Time.time;

        if (castSound != null)
            AudioSource.PlayClipAtPoint(castSound, originData.position);

        ExecuteSpellEffect(originData, context);
    }

    public virtual GameObject CreatePreparationEffect(SpellCastContext context)
    {
        if (preparationPrefab == null) return null;

        var originData = CalculateOriginData(context);
        return Instantiate(preparationPrefab, originData.position, originData.rotation);
    }

    public GameObject CreateChargingEffect(Transform attachPoint)
    {
        if (chargingEffectPrefab == null) return null;

        activeChargingEffect = Instantiate(chargingEffectPrefab, attachPoint.position, attachPoint.rotation, attachPoint);
        return activeChargingEffect;
    }

    public void StopChargingEffect()
    {
        if (activeChargingEffect != null)
        {
            Destroy(activeChargingEffect);
            activeChargingEffect = null;
        }
    }

    public void CreateCancelEffect(Vector3 position, Quaternion rotation)
    {
        if (cancelEffectPrefab == null) return;

        var effect = Instantiate(cancelEffectPrefab, position, rotation);
        var ps = effect.GetComponent<ParticleSystem>();

        float destroyTime = 2f;
        if (ps != null)
            destroyTime = ps.main.duration + ps.main.startLifetime.constantMax;

        Destroy(effect, destroyTime);
    }

    public virtual void ResetCooldown()
    {
        lastCastTime = -999f;
    }

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

    #region Sistema de Escalado

    public SpellScalingResult CalculateScaledProperties(float scale, float availableMana = float.MaxValue)
    {
        scale = Mathf.Clamp(scale, minScale, maxScale);

        // Verificar cache
        float cacheKey = Mathf.Round(scale * 100f) / 100f;
        if (_scalingCache.TryGetValue(cacheKey, out var cachedResult))
        {
            var result = cachedResult;
            result.isViable = result.finalManaCost <= availableMana;
            return result;
        }

        // Calcular nuevo resultado
        var newResult = new SpellScalingResult(scale)
        {
            primaryPropertyValue = primaryPropertyBaseValue * EvaluateCurve(primaryPropertyCurve, scale, 1f),
            speedMultiplier = EvaluateCurve(speedCurve, scale, 1f),
            finalManaCost = manaCost * EvaluateCurve(manaCostCurve, scale, 1f)
        };

        if (secondaryProperty != ScalableProperty.NONE)
        {
            newResult.secondaryPropertyValue = secondaryPropertyBaseValue * EvaluateCurve(secondaryPropertyCurve, scale, 1f);
        }

        newResult.isViable = newResult.finalManaCost <= availableMana;

        // Cachear resultado
        _scalingCache[cacheKey] = newResult;

        return newResult;
    }

    public float FindMaxViableScale(float desiredScale, float availableMana)
    {
        if (!allowScaling) return 1.0f;

        var desiredResult = CalculateScaledProperties(desiredScale, availableMana);
        if (desiredResult.isViable) return desiredScale;

        // Búsqueda binaria
        float low = minScale;
        float high = desiredScale;
        float viable = 1.0f;

        for (int i = 0; i < 10; i++)
        {
            float mid = (low + high) * 0.5f;
            var result = CalculateScaledProperties(mid, availableMana);

            if (result.isViable)
            {
                viable = mid;
                low = mid;
            }
            else
            {
                high = mid;
            }

            if (Mathf.Abs(high - low) < 0.01f) break;
        }

        return viable;
    }

    public string GetScalingEfficiencyDescription(float scale)
    {
        if (!allowScaling) return "Sin escalado";

        if (scale >= 0.9f && scale <= 1.1f) return "Óptimo";
        if (scale <= optimalScaleThreshold && scale >= (1f / optimalScaleThreshold)) return "Eficiente";
        if (scale <= maxScale * 0.8f && scale >= minScale * 1.2f) return "Moderado";
        return "Ineficiente";
    }

    public Color GetScalingEfficiencyColor(float scale)
    {
        switch (GetScalingEfficiencyDescription(scale))
        {
            case "Óptimo": return Color.green;
            case "Eficiente": return Color.yellow;
            case "Moderado": return new Color(1f, 0.5f, 0f);
            case "Ineficiente": return Color.red;
            default: return Color.white;
        }
    }

    public static AnimationCurve CreateDefaultCurveForProperty(ScalableProperty property)
    {
        switch (property)
        {
            case ScalableProperty.DAMAGE:
            case ScalableProperty.HEALING:
                return AnimationCurve.EaseInOut(0.5f, 1.4f, 4f, 0.3f);

            case ScalableProperty.SHIELD_STRENGTH:
                return AnimationCurve.EaseInOut(0.5f, 1.2f, 4f, 0.6f);

            case ScalableProperty.BOOST:
                return AnimationCurve.EaseInOut(0.5f, 1.3f, 4f, 0.4f);

            case ScalableProperty.PROJECTILE_SPEED:
                return AnimationCurve.EaseInOut(0.5f, 1.5f, 4f, 0.2f);

            case ScalableProperty.DURATION:
                return AnimationCurve.EaseInOut(0.5f, 1.1f, 4f, 0.8f);

            default:
                return AnimationCurve.EaseInOut(0.5f, 1.2f, 4f, 0.5f);
        }
    }

    #endregion

    #region Métodos Protegidos

    protected abstract void ExecuteSpellEffect(OriginData origin, SpellCastContext context);

    #endregion

    #region Métodos Privados

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
                basePosition = context.playerTransform.position + Vector3.up * 1.2f;
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
                              context.playerTransform.forward * originConfig.distanceInFrontOfPlayer +
                              Vector3.up * 1.2f;
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
                    basePosition = context.staffTransform.position;
                    baseRotation = context.staffTransform.rotation;
                }
                break;

            case SpellOriginType.TARGET_ABOVE:
                if (context.hasValidTarget)
                {
                    basePosition = context.targetPosition + Vector3.up * originConfig.heightAboveTarget;
                    baseRotation = Quaternion.LookRotation(Vector3.down);
                }
                else
                {
                    basePosition = context.staffTransform.position + Vector3.up * originConfig.heightAboveTarget;
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
                    basePosition = context.playerTransform.position;
                    baseRotation = context.playerTransform.rotation;
                }
                break;

            case SpellOriginType.WORLD_FIXED:
                basePosition = originConfig.worldFixedPosition;
                baseRotation = Quaternion.identity;
                break;
        }

        Vector3 finalPosition = basePosition + baseRotation * originConfig.positionOffset;
        Quaternion finalRotation = baseRotation * Quaternion.Euler(originConfig.rotationOffset);

        return new OriginData(finalPosition, finalRotation, referenceTransform);
    }

    private float EvaluateCurve(AnimationCurve curve, float scale, float defaultValue)
    {
        if (curve == null || curve.length == 0) return defaultValue;
        return curve.Evaluate(scale);
    }

    #endregion

    #region Métodos Obsoletos (Compatibilidad)

    [System.Obsolete("Usa Cast(SpellCastContext) en su lugar")]
    public virtual void Cast(Transform origin, PlayerStatsManager caster)
    {
        var context = new SpellCastContext(origin, caster.transform, caster);
        Cast(context);
    }

    [System.Obsolete("Usa ExecuteSpellEffect(OriginData, SpellCastContext) en su lugar")]
    protected virtual void ExecuteSpellEffect(Transform origin, PlayerStatsManager caster) { }

    #endregion
}