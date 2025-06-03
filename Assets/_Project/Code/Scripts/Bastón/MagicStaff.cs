using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using System;
using System.Collections.Generic;
using static SpellBase;
using System.Collections;
using UnityEngine.InputSystem;

/// <summary>
/// Estados del sistema de hechizos
/// </summary>
public enum SpellState
{
    IDLE,           // No hay hechizo activo
    CHARGING,       // Cargando hechizo (manteniendo gatillo)
    PREPARED,       // Hechizo listo, esperando comando
    EXECUTING       // Ejecutando hechizo
}

/// <summary>
/// Estrategias para escalar partículas en el preparation prefab
/// </summary>
public enum ParticleScalingStrategy
{
    [System.ComponentModel.Description("No escalar las partículas (mantener tamaño original)")]
    NO_SCALING,

    [System.ComponentModel.Description("Escalado suave (raíz cuadrada del scale)")]
    SOFT_SCALING,

    [System.ComponentModel.Description("Escalado limitado (máximo 150% del tamaño original)")]
    LIMITED_SCALING,

    [System.ComponentModel.Description("Escalado completo (escala directa 1:1)")]
    FULL_SCALING,

    [System.ComponentModel.Description("Solo escalar emisión, no tamaño")]
    EMISSION_ONLY
}

public class MagicStaff : MonoBehaviour
{
    #region Variables y Referencias

    [Header("━━━ Configuración Principal ━━━")]
    [Tooltip("Punto desde donde se generan los hechizos")]
    [SerializeField] private Transform spellSpawnPoint;

    [Tooltip("Hechizo actualmente equipado en el bastón")]
    [SerializeField] private SpellBase equippedSpell;

    [Header("━━━ Efectos Visuales ━━━")]
    [Tooltip("Partículas que aparecen al lanzar un hechizo")]
    [SerializeField] private ParticleSystem castingVFX;

    [Tooltip("Partículas durante la carga del hechizo")]
    [SerializeField] private ParticleSystem chargingVFX;

    [Tooltip("Partículas al cancelar un hechizo")]
    [SerializeField] private ParticleSystem cancelVFX;

    [Header("━━━ Sistema de Audio ━━━")]
    [SerializeField] private AudioController audioController;

    [Space(10)]
    [Tooltip("ID del sonido de carga")]
    [SerializeField] private string chargingSoundId = "staff_charging";

    [Tooltip("ID del sonido de lanzamiento")]
    [SerializeField] private string castSoundId = "staff_cast";

    [Tooltip("ID del sonido de cancelación")]
    [SerializeField] private string cancelSoundId = "staff_cancel";

    [Header("━━━ Sistema de Apuntado ━━━")]
    [Tooltip("Capas que detecta el raycast para apuntar")]
    [SerializeField] private LayerMask raycastLayers = -1;

    [Tooltip("Distancia máxima del raycast (metros)")]
    [SerializeField] private float maxRaycastDistance = 50f;

    [Tooltip("¿El raycast apunta hacia arriba en lugar de adelante?")]
    [SerializeField] private bool useUpwardRaycast = false;

    [Space(5)]
    [Tooltip("Origen del raycast. Si está vacío, usa spellSpawnPoint")]
    [SerializeField] private Transform raycastOrigin;

    [Tooltip("Usar rotación del raycastOrigin para la dirección")]
    [SerializeField] private bool useRaycastOriginRotation = true;

    [Header("━━━ Sistema de Comandos ━━━")]
    [Tooltip("Velocidad mínima para detectar gestos (m/s)")]
    [SerializeField] private float gestureMinSpeed = 2f;

    [Tooltip("Tiempo entre gestos para evitar detecciones múltiples")]
    [SerializeField] private float gestureCooldown = 0.5f;

    [Header("━━━ Sistema de Escalado ━━━")]
    [Tooltip("Punto de referencia para medir distancia")]
    [SerializeField] private Transform scalingReferencePoint;

    [Space(5)]
    [Tooltip("Distancia para escala 1.0 (metros)")]
    [Range(0.1f, 1f)]
    [SerializeField] private float baseScalingDistance = 0.3f;

    [Tooltip("Distancia mínima = escala mínima (metros)")]
    [Range(0.05f, 0.5f)]
    [SerializeField] private float minScalingDistance = 0.1f;

    [Tooltip("Distancia máxima = escala máxima (metros)")]
    [Range(0.5f, 2f)]
    [SerializeField] private float maxScalingDistance = 1f;

    [Space(5)]
    [Tooltip("Botón para activar escalado en mano no dominante")]
    [SerializeField] private InputActionReference scalingActivationAction;

    [Header("━━━ Escalado de Partículas ━━━")]
    [Tooltip("Cómo se escalan las partículas del efecto de preparación")]
    [SerializeField] private ParticleScalingStrategy particleScalingStrategy = ParticleScalingStrategy.SOFT_SCALING;

    [Tooltip("Factor máximo de escalado para partículas")]
    [Range(1f, 3f)]
    [SerializeField] private float maxParticleScaleFactor = 1.5f;

    [Header("━━━ Opciones de Debug ━━━")]
    [SerializeField] private bool showDebugMessages = false;
    [SerializeField] private bool showTargetingDebug = false;
    [SerializeField] private bool showCommandDebug = false;
    [SerializeField] private bool showScalingDebug = false;

    // ─── Referencias de Componentes (No Serializadas) ───
    private XRGrabInteractable grabInteractable;
    private readonly List<XRBaseController> heldByControllers = new List<XRBaseController>();
    private PlayerStatsManager playerStats;

    // ─── Estado del Sistema de Hechizos ───
    private SpellState currentState = SpellState.IDLE;
    private bool isCharging = false;
    private XRBaseController chargingController = null;
    private float chargeStartTime;
    private bool chargeComplete;
    private float lastProgress = -1f;

    // ─── Estado de Preparación ───
    private GameObject preparationEffect = null;
    private float preparationStartTime;
    private SpellCastContext currentContext;

    // ─── Sistema de Targeting ───
    private Vector3 lastTargetPosition;
    private float targetingStartTime;
    private bool isTargeting = false;

    // ─── Sistema de Detección de Gestos ───
    private Vector3 lastControllerPosition;
    private Vector3 lastControllerVelocity;
    private float lastGestureTime = -999f;

    // ─── Gestión de Efectos Visuales ───
    private readonly List<GameObject> activeCircles = new List<GameObject>();
    private readonly List<VFXCircleEffect> activeEffects = new List<VFXCircleEffect>();

    // ─── Sistema de Escalado ───
    private bool isScalingActive = false;
    private float currentSpellScale = 1f;
    private float targetSpellScale = 1f;
    private XRBaseController nonDominantController = null;
    private bool isScalingButtonPressed = false;

    // ─── Cache de Controladores ───
    private readonly List<XRBaseController> allControllers = new List<XRBaseController>();
    private float lastControllerCacheUpdate = 0f;
    private const float CONTROLLER_CACHE_LIFETIME = 1f;

    // ─── Cache de Propiedades de Partículas ───
    private readonly Dictionary<ParticleSystem, ParticleSystemOriginalProperties> originalParticleProperties =
        new Dictionary<ParticleSystem, ParticleSystemOriginalProperties>();

    // ─── Eventos Públicos ───
    public event Action<SpellBase> OnSpellCast;
    public event Action<bool> OnSpellChargeStateChanged;
    public event Action<SpellState> OnSpellStateChanged;

    #endregion
    #region Inicialización y Configuración

    private void Awake()
    {
        // Obtener componente de interacción VR
        grabInteractable = GetComponent<XRGrabInteractable>();

        // Validar componentes críticos
        if (grabInteractable == null)
        {
            Debug.LogError($"[MagicStaff] Falta componente XRGrabInteractable en {gameObject.name}");
            enabled = false;
            return;
        }

        // Configurar punto de generación por defecto si no se asignó
        if (spellSpawnPoint == null)
        {
            spellSpawnPoint = transform;
            if (showDebugMessages)
                Debug.LogWarning("[MagicStaff] Punto de generación no asignado. Usando transform del bastón.");
        }

        // Validar sistema de escalado
        ValidateScalingConfiguration();
    }

    private void OnEnable()
    {
        // Suscribir eventos de interacción VR
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.AddListener(OnGrabbed);
            grabInteractable.selectExited.AddListener(OnReleased);
        }

        // Configurar sistema de escalado
        if (scalingActivationAction != null)
        {
            scalingActivationAction.action.Enable();
            scalingActivationAction.action.started += OnScalingActivationStarted;
            scalingActivationAction.action.canceled += OnScalingActivationEnded;
        }
    }

    private void OnDisable()
    {
        // Desuscribir eventos de interacción VR
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnGrabbed);
            grabInteractable.selectExited.RemoveListener(OnReleased);
        }

        // Desuscribir eventos de escalado
        if (scalingActivationAction != null)
        {
            scalingActivationAction.action.Disable();
            scalingActivationAction.action.started -= OnScalingActivationStarted;
            scalingActivationAction.action.canceled -= OnScalingActivationEnded;
        }

        // Limpiar estado completo
        ResetAllState();
    }

    /// <summary>
    /// Valida la configuración del sistema de escalado
    /// </summary>
    private void ValidateScalingConfiguration()
    {
        // Usar spellSpawnPoint como fallback para referencia de escalado
        if (scalingReferencePoint == null)
        {
            scalingReferencePoint = spellSpawnPoint;
            if (showDebugMessages)
                Debug.Log("[MagicStaff] Sin punto de referencia de escalado. Usando punto de generación.");
        }

        // Validar rangos de distancia
        if (minScalingDistance >= maxScalingDistance)
        {
            Debug.LogWarning("[MagicStaff] Distancias de escalado incorrectas. Ajustando automáticamente.");
            minScalingDistance = 0.1f;
            baseScalingDistance = 0.3f;
            maxScalingDistance = 1f;
        }
    }

    /// <summary>
    /// Resetea completamente el estado del bastón
    /// </summary>
    private void ResetAllState()
    {
        // Cambiar a estado inactivo
        SetSpellState(SpellState.IDLE);

        // Limpiar estado de carga
        isCharging = false;
        chargingController = null;
        chargeComplete = false;
        lastProgress = -1f;

        // Limpiar listas de controladores
        heldByControllers.Clear();

        // Limpiar estado de targeting
        isTargeting = false;

        // Limpiar estado de escalado
        ResetScalingState();

        // Limpiar todos los efectos visuales
        CleanupAllVisualEffects();
    }

    /// <summary>
    /// Limpia todos los efectos visuales activos
    /// </summary>
    private void CleanupAllVisualEffects()
    {
        StopAllParticleEffects();
        ClearAllCircles();
        ClearPreparationEffect();
    }

    #endregion
    #region Gestión de Interacción VR

    /// <summary>
    /// Se ejecuta cuando el bastón es agarrado por un controlador VR
    /// </summary>
    public void OnGrabbed(SelectEnterEventArgs args)
    {
        var newController = args.interactorObject.transform.GetComponent<XRBaseController>();
        if (newController == null) return;

        // Evitar duplicados
        if (heldByControllers.Contains(newController)) return;

        // Añadir a la lista de controladores
        heldByControllers.Add(newController);

        // Si es la mano dominante, obtener referencia al jugador
        var spellController = newController.GetComponent<SpellCastController>();
        if (spellController != null && spellController.IsDominantHand)
        {
            playerStats = newController.transform.root.GetComponent<PlayerStatsManager>();

            if (showDebugMessages)
                Debug.Log($"[MagicStaff] Bastón agarrado por mano dominante: {newController.name}");
        }
    }

    /// <summary>
    /// Se ejecuta cuando el bastón es soltado
    /// </summary>
    public void OnReleased(SelectExitEventArgs args)
    {
        var releasedController = args.interactorObject.transform.GetComponent<XRBaseController>();
        if (releasedController == null) return;

        // Remover de la lista
        heldByControllers.Remove(releasedController);

        // Si era el controlador activo, cancelar cualquier actividad
        if (IsActiveController(releasedController))
        {
            if (showDebugMessages)
                Debug.Log("[MagicStaff] Controlador activo soltó el bastón - cancelando actividad");

            CancelCurrentSpell();
        }

        // Actualizar referencia a playerStats
        UpdatePlayerStatsReference();
    }

    /// <summary>
    /// Verifica si este bastón está siendo sostenido por un controlador específico
    /// </summary>
    public bool IsHeldBy(XRBaseController controller)
    {
        return controller != null && heldByControllers.Contains(controller);
    }

    /// <summary>
    /// Verifica si este bastón está siendo sostenido por la mano dominante
    /// </summary>
    public bool IsHeldByDominantHand(XRBaseController controller)
    {
        if (!IsHeldBy(controller)) return false;

        var spellController = controller.GetComponent<SpellCastController>();
        return spellController != null && spellController.IsDominantHand;
    }

    /// <summary>
    /// Verifica si el controlador es el que está activamente usando el bastón
    /// </summary>
    private bool IsActiveController(XRBaseController controller)
    {
        return (isCharging || currentState != SpellState.IDLE) && controller == chargingController;
    }

    /// <summary>
    /// Actualiza la referencia al PlayerStatsManager basado en los controladores actuales
    /// </summary>
    private void UpdatePlayerStatsReference()
    {
        playerStats = null;

        // Si no hay controladores, no hay jugador
        if (heldByControllers.Count == 0) return;

        // Buscar el primer controlador dominante
        foreach (var controller in heldByControllers)
        {
            var spellController = controller.GetComponent<SpellCastController>();
            if (spellController != null && spellController.IsDominantHand)
            {
                playerStats = controller.transform.root.GetComponent<PlayerStatsManager>();
                break;
            }
        }
    }

    #endregion
    #region Sistema de Estados

    /// <summary>
    /// Cambia el estado actual del sistema de hechizos
    /// </summary>
    private void SetSpellState(SpellState newState)
    {
        if (currentState == newState) return;

        var oldState = currentState;
        currentState = newState;

        // Log solo si debug de comandos está activo
        if (showCommandDebug)
            Debug.Log($"[MagicStaff] Estado: {oldState} → {newState}");

        // Notificar cambio de estado
        OnSpellStateChanged?.Invoke(newState);
    }

    /// <summary>
    /// Cancela cualquier actividad de hechizo en curso
    /// </summary>
    public void CancelCurrentSpell()
    {
        switch (currentState)
        {
            case SpellState.CHARGING:
                CancelCharging(chargingController);
                break;

            case SpellState.PREPARED:
                CancelPreparation();
                break;

            case SpellState.EXECUTING:
                // Los hechizos en ejecución no se pueden cancelar
                break;
        }

        SetSpellState(SpellState.IDLE);
    }

    /// <summary>
    /// Obtiene el estado actual del sistema
    /// </summary>
    public SpellState GetCurrentState() => currentState;

    /// <summary>
    /// Verifica si el bastón está listo para lanzar hechizos
    /// </summary>
    public bool IsReadyToCast() => currentState == SpellState.IDLE && equippedSpell != null;

    /// <summary>
    /// Verifica si hay un hechizo en progreso
    /// </summary>
    public bool IsSpellInProgress() => currentState != SpellState.IDLE;

    #endregion
    #region Sistema de Carga

    /// <summary>
    /// Inicia la carga de un hechizo
    /// </summary>
    public void StartCharging(XRBaseController requestingController)
    {
        // Validar que es la mano dominante
        if (!IsHeldByDominantHand(requestingController))
            return;

        // Validar hechizo equipado
        if (equippedSpell == null)
        {
            if (showDebugMessages)
                Debug.LogError("[MagicStaff] No hay hechizo equipado");
            PlayFailedCastFeedback();
            return;
        }

        // Validar mana suficiente
        if (!HasEnoughMana())
        {
            if (showDebugMessages)
                Debug.Log("[MagicStaff] Mana insuficiente");
            PlayInsufficientManaFeedback();
            return;
        }

        // Cancelar actividad anterior si existe
        if (currentState != SpellState.IDLE)
            CancelCurrentSpell();

        // Inicializar estado de carga
        InitializeCharging(requestingController);

        // Crear efectos visuales
        CreateChargingEffects();

        // Notificar inicio de carga
        OnSpellChargeStateChanged?.Invoke(true);
    }

    /// <summary>
    /// Finaliza la carga y determina siguiente acción
    /// </summary>
    public void FinishCharging(XRBaseController requestingController, float chargeTime)
    {
        // Validar controlador
        if (!IsValidChargingController(requestingController))
            return;

        // Detener efectos de carga
        isCharging = false;
        PlayChargingEffects(false);

        // Validar condiciones de lanzamiento
        if (!ValidateSpellCasting(chargeTime))
        {
            SetSpellState(SpellState.IDLE);
            return;
        }

        // Decidir siguiente acción según tipo de comando
        if (equippedSpell.CommandType == SpellCommandType.INSTANT)
        {
            ExecuteSpellDirectly();
        }
        else
        {
            StartPreparation();
        }
    }

    /// <summary>
    /// Cancela la carga actual
    /// </summary>
    public void CancelCharging(XRBaseController requestingController)
    {
        // Validar que es el controlador correcto
        if (!IsValidChargingController(requestingController) && !IsHeldByDominantHand(requestingController))
            return;

        if (!isCharging) return;

        // Limpiar estado
        isCharging = false;
        chargeComplete = false;
        lastProgress = -1f;
        chargingController = null;

        // Detener efectos
        PlayChargingEffects(false);
        PlayCancelEffects();
        StartCoroutine(FadeOutCircles());
        ClearPreparationEffect();
        ResetScalingState();

        // Cambiar estado
        SetSpellState(SpellState.IDLE);
        OnSpellChargeStateChanged?.Invoke(false);
    }

    /// <summary>
    /// Muestra el efecto de preparación cuando la carga se completa
    /// </summary>
    private void ShowPreparationEffect()
    {
        // Solo crear si no existe y el hechizo lo requiere
        if (preparationEffect != null || !equippedSpell.HasPreparationPrefab)
            return;

        // Crear contexto y efecto
        var tempContext = CreateSpellContext();
        preparationEffect = equippedSpell.CreatePreparationEffect(tempContext);

        if (showDebugMessages && preparationEffect == null)
            Debug.LogError("[MagicStaff] Error al crear efecto de preparación");
    }

    // ─── Métodos Auxiliares ───

    /// <summary>
    /// Inicializa las variables de carga
    /// </summary>
    private void InitializeCharging(XRBaseController controller)
    {
        SetSpellState(SpellState.CHARGING);
        isCharging = true;
        chargingController = controller;
        chargeStartTime = Time.time;
        chargeComplete = false;
        lastProgress = -1f;
    }

    /// <summary>
    /// Crea los efectos visuales de carga
    /// </summary>
    private void CreateChargingEffects()
    {
        // Limpiar efectos anteriores
        ClearAllCircles();

        // Crear círculos mágicos si existen
        if (equippedSpell.HasMagicCircles())
        {
            foreach (var config in equippedSpell.GetMagicCircles())
            {
                if (config.circlePrefab != null)
                    StartCoroutine(CreateDelayedCircle(config));
            }
        }

        // Activar partículas de carga
        PlayChargingEffects(true);
    }

    /// <summary>
    /// Valida si hay suficiente mana para el hechizo
    /// </summary>
    private bool HasEnoughMana()
    {
        return playerStats != null && playerStats.Mana.CanCastSpell(equippedSpell.ManaCost);
    }

    /// <summary>
    /// Valida si el controlador es válido para la carga
    /// </summary>
    private bool IsValidChargingController(XRBaseController controller)
    {
        return chargingController == controller && isCharging;
    }

    /// <summary>
    /// Valida todas las condiciones para lanzar el hechizo
    /// </summary>
    private bool ValidateSpellCasting(float chargeTime)
    {
        // Verificar hechizo
        if (equippedSpell == null)
        {
            PlayFailedCastFeedback();
            return false;
        }

        // Verificar tiempo mínimo de carga
        if (chargeTime < equippedSpell.MinChargeTime)
        {
            if (showDebugMessages)
                Debug.Log($"[MagicStaff] Carga insuficiente: {chargeTime:F1}s < {equippedSpell.MinChargeTime:F1}s");
            PlayCancelEffects();
            return false;
        }

        // Verificar mana
        if (!HasEnoughMana())
        {
            PlayInsufficientManaFeedback();
            return false;
        }

        // Verificar cooldown
        if (!equippedSpell.IsReady())
        {
            PlayCooldownFeedback();
            return false;
        }

        return true;
    }

    #endregion
    #region Sistema de Preparación

    /// <summary>
    /// Inicia la fase de preparación del hechizo
    /// </summary>
    private void StartPreparation()
    {
        SetSpellState(SpellState.PREPARED);
        preparationStartTime = Time.time;

        // Desactivar escalado al entrar en preparación
        if (isScalingActive)
        {
            if (showScalingDebug)
                Debug.Log($"[MagicStaff] Escalado finalizado: {currentSpellScale:F2}x");

            DeactivateScaling();
        }

        // Crear contexto del hechizo
        currentContext = CreateSpellContext();

        // Crear efecto de preparación si es necesario
        if (preparationEffect == null && equippedSpell.HasPreparationPrefab)
        {
            preparationEffect = equippedSpell.CreatePreparationEffect(currentContext);
        }

        // Inicializar sistema específico según tipo de comando
        InitializeCommandSystem();
    }

    /// <summary>
    /// Cancela la preparación actual
    /// </summary>
    private void CancelPreparation()
    {
        if (currentState != SpellState.PREPARED) return;

        // Limpiar efectos
        ClearPreparationEffect();
        StopTargeting();
        StartCoroutine(FadeOutCircles());

        SetSpellState(SpellState.IDLE);
    }

    /// <summary>
    /// Inicializa el sistema específico según el tipo de comando
    /// </summary>
    private void InitializeCommandSystem()
    {
        switch (equippedSpell.CommandType)
        {
            case SpellCommandType.DIRECTIONAL:
                StartTargeting();
                break;

            case SpellCommandType.EMERGE:
            case SpellCommandType.DESCEND:
                // Inicializar tracking de velocidad para gestos
                lastControllerPosition = chargingController.transform.position;
                lastControllerVelocity = Vector3.zero;
                break;
        }

        if (showCommandDebug)
            Debug.Log($"[MagicStaff] Hechizo preparado - Comando: {equippedSpell.CommandType}");
    }

    /// <summary>
    /// Desactiva el sistema de escalado manteniendo la escala actual
    /// </summary>
    private void DeactivateScaling()
    {
        isScalingActive = false;
        isScalingButtonPressed = false;
        nonDominantController = null;
    }

    #endregion
    #region Sistema de Targeting Direccional

    /// <summary>
    /// Inicia el sistema de targeting direccional
    /// </summary>
    private void StartTargeting()
    {
        isTargeting = false;
        lastTargetPosition = Vector3.zero;
        targetingStartTime = 0f;
    }

    /// <summary>
    /// Detiene el sistema de targeting
    /// </summary>
    private void StopTargeting()
    {
        isTargeting = false;
    }

    /// <summary>
    /// Actualiza el sistema de targeting (llamado desde Update)
    /// </summary>
    private void UpdateTargeting()
    {
        if (currentState != SpellState.PREPARED ||
            equippedSpell.CommandType != SpellCommandType.DIRECTIONAL)
            return;

        // Obtener posición objetivo actual
        if (!GetCurrentTargetPosition(out Vector3 currentTargetPosition))
        {
            ResetTargeting();
            return;
        }

        // Verificar estabilidad del objetivo
        float distance = Vector3.Distance(currentTargetPosition, lastTargetPosition);

        if (distance <= equippedSpell.AimTolerance)
        {
            HandleStableTarget(currentTargetPosition);
        }
        else
        {
            HandleMovingTarget(currentTargetPosition, distance);
        }
    }

    /// <summary>
    /// Ejecuta un hechizo direccional con la posición objetivo
    /// </summary>
    private void ExecuteDirectionalSpell(Vector3 targetPosition)
    {
        // Actualizar contexto con información precisa
        currentContext.targetPosition = targetPosition;
        currentContext.hasValidTarget = true;
        currentContext.commandUsed = SpellCommandType.DIRECTIONAL;
        currentContext.commandDirection = (targetPosition - spellSpawnPoint.position).normalized;
        currentContext.commandIntensity = 1f; // Máxima precisión

        if (showCommandDebug)
            Debug.Log($"[MagicStaff] Hechizo direccional PRECISO → {targetPosition}");

        ExecuteSpellWithContext(currentContext);
    }

    // ─── Métodos Auxiliares ───

    /// <summary>
    /// Resetea el estado de targeting cuando no hay objetivo válido
    /// </summary>
    private void ResetTargeting()
    {
        if (isTargeting)
        {
            isTargeting = false;
            if (showCommandDebug)
                Debug.Log("[MagicStaff] Targeting perdido - sin objetivo válido");
        }
    }

    /// <summary>
    /// Maneja cuando el objetivo se mantiene estable
    /// </summary>
    private void HandleStableTarget(Vector3 targetPosition)
    {
        if (!isTargeting)
        {
            // Iniciar targeting
            isTargeting = true;
            targetingStartTime = Time.time;
            lastTargetPosition = targetPosition;

            if (showCommandDebug)
                Debug.Log($"[MagicStaff] Targeting iniciado en: {targetPosition}");
        }
        else
        {
            // Verificar si completó el tiempo requerido
            float holdTime = Time.time - targetingStartTime;
            if (holdTime >= equippedSpell.AimHoldTime)
            {
                ExecuteDirectionalSpell(targetPosition);
            }
        }
    }

    /// <summary>
    /// Maneja cuando el objetivo se está moviendo
    /// </summary>
    private void HandleMovingTarget(Vector3 currentPosition, float distance)
    {
        if (isTargeting)
        {
            isTargeting = false;
            if (showCommandDebug)
                Debug.Log($"[MagicStaff] Targeting resetado - movimiento excesivo ({distance:F2}m)");
        }

        lastTargetPosition = currentPosition;
    }

    /// <summary>
    /// Obtiene la posición objetivo actual basada en el raycast
    /// </summary>
    private bool GetCurrentTargetPosition(out Vector3 targetPosition)
    {
        // Configurar origen y dirección del raycast
        Transform origin = raycastOrigin ?? spellSpawnPoint;
        Transform rotationSource = (useRaycastOriginRotation && raycastOrigin != null) ? raycastOrigin : spellSpawnPoint;
        Vector3 rayDirection = useUpwardRaycast ? rotationSource.up : rotationSource.forward;

        // Realizar raycast
        if (Physics.Raycast(origin.position, rayDirection, out RaycastHit hitInfo, maxRaycastDistance, raycastLayers))
        {
            targetPosition = hitInfo.point;
            return true;
        }

        // No hay hit - usar punto máximo
        targetPosition = origin.position + rayDirection * maxRaycastDistance;
        return false;
    }

    #endregion
    #region Sistema de Detección de Gestos

    /// <summary>
    /// Actualiza la detección de gestos (llamado desde Update)
    /// </summary>
    private void UpdateGestureDetection()
    {
        if (currentState != SpellState.PREPARED || chargingController == null)
            return;

        // Solo detectar gestos para hechizos que los requieren
        var commandType = equippedSpell.CommandType;
        if (commandType != SpellCommandType.EMERGE && commandType != SpellCommandType.DESCEND)
            return;

        // Verificar cooldown entre gestos
        if (Time.time - lastGestureTime < gestureCooldown)
        {
            UpdateControllerTracking();
            return;
        }

        // Detectar y ejecutar gesto
        DetectAndExecuteGesture(commandType);
    }

    /// <summary>
    /// Ejecuta un hechizo activado por gesto
    /// </summary>
    private void ExecuteGestureSpell(SpellCommandType gestureType, Vector3 direction, float intensity)
    {
        if (showCommandDebug)
            Debug.Log($"[MagicStaff] Gesto {gestureType} detectado - Intensidad: {intensity:F1}");

        // Actualizar contexto con información del gesto
        currentContext.commandUsed = gestureType;
        currentContext.commandDirection = direction;
        currentContext.commandIntensity = intensity;

        ExecuteSpellWithContext(currentContext);
    }

    // ─── Métodos Auxiliares ───

    /// <summary>
    /// Actualiza el tracking del controlador
    /// </summary>
    private void UpdateControllerTracking()
    {
        var currentPosition = chargingController.transform.position;
        lastControllerVelocity = (currentPosition - lastControllerPosition) / Time.deltaTime;
        lastControllerPosition = currentPosition;
    }

    /// <summary>
    /// Detecta y ejecuta gestos según el tipo de comando
    /// </summary>
    private void DetectAndExecuteGesture(SpellCommandType commandType)
    {
        // Calcular velocidad actual
        var currentPosition = chargingController.transform.position;
        var currentVelocity = (currentPosition - lastControllerPosition) / Time.deltaTime;

        // Verificar velocidad consistente (frame actual y anterior)
        bool consistentVelocity = false;
        float velocityMagnitude = 0f;

        switch (commandType)
        {
            case SpellCommandType.EMERGE:
                consistentVelocity = currentVelocity.y > gestureMinSpeed &&
                                    lastControllerVelocity.y > gestureMinSpeed;
                velocityMagnitude = currentVelocity.y;
                break;

            case SpellCommandType.DESCEND:
                consistentVelocity = currentVelocity.y < -gestureMinSpeed &&
                                    lastControllerVelocity.y < -gestureMinSpeed;
                velocityMagnitude = Mathf.Abs(currentVelocity.y);
                break;
        }

        // Ejecutar si se detectó el gesto
        if (consistentVelocity)
        {
            var direction = commandType == SpellCommandType.EMERGE ? Vector3.up : Vector3.down;
            ExecuteGestureSpell(commandType, direction, velocityMagnitude);
            lastGestureTime = Time.time;
        }

        // Actualizar estado para siguiente frame
        UpdateControllerTracking();
    }

    #endregion
    #region Ejecución de Hechizos

    /// <summary>
    /// Ejecuta un hechizo inmediatamente (sin preparación)
    /// </summary>
    private void ExecuteSpellDirectly()
    {
        SetSpellState(SpellState.EXECUTING);

        // Crear contexto y ejecutar
        var context = CreateSpellContext();
        context.commandUsed = SpellCommandType.INSTANT;

        ExecuteSpellWithContext(context);
    }

    /// <summary>
    /// Ejecuta el hechizo con el contexto dado
    /// </summary>
    private void ExecuteSpellWithContext(SpellCastContext context)
    {
        SetSpellState(SpellState.EXECUTING);

        // Ajustar escalado si es necesario
        AdjustScalingForMana(ref context);

        // Modificar contexto para hechizos direccionales con efecto de preparación
        if (ShouldUsePreparationPosition(context))
        {
            context = CreateModifiedContextForPreparationEffect(context);
        }

        // Limpiar efectos antes de ejecutar
        CleanupPreExecutionEffects();

        // Consumir mana
        float finalManaCost = context.wasScaled ? context.scalingResult.finalManaCost : equippedSpell.ManaCost;
        playerStats.Mana.CastSpell(finalManaCost);

        // Ejecutar el hechizo
        equippedSpell.Cast(context);

        // Efectos post-lanzamiento
        PlayCastingEffects();
        ResetScalingState();
        ResetSpellState();

        // Notificar lanzamiento
        OnSpellCast?.Invoke(equippedSpell);
    }

    /// <summary>
    /// Crea el contexto actual del hechizo
    /// </summary>
    private SpellCastContext CreateSpellContext()
    {
        var context = new SpellCastContext(spellSpawnPoint, playerStats.transform, playerStats);

        // Obtener información del objetivo
        if (GetCurrentTargetPosition(out Vector3 targetPosition))
        {
            UpdateContextWithTarget(ref context, targetPosition);
        }

        // Añadir información de escalado si aplica
        if (equippedSpell != null && equippedSpell.AllowScaling)
        {
            context.UpdateScaling(currentSpellScale, equippedSpell);
        }

        return context;
    }

    // ─── Métodos Auxiliares ───

    /// <summary>
    /// Ajusta el escalado del hechizo según el mana disponible
    /// </summary>
    private void AdjustScalingForMana(ref SpellCastContext context)
    {
        if (!context.wasScaled || !equippedSpell.AllowScaling) return;

        float availableMana = playerStats.Mana.GetCurrentValue();
        float requiredMana = context.scalingResult.finalManaCost;

        if (requiredMana > availableMana)
        {
            float maxViableScale = equippedSpell.FindMaxViableScale(context.spellScale, availableMana);

            if (showDebugMessages)
                Debug.Log($"[MagicStaff] Ajustando escala: {context.spellScale:F2}x → {maxViableScale:F2}x (mana insuficiente)");

            context.UpdateScaling(maxViableScale, equippedSpell);
        }
    }

    /// <summary>
    /// Verifica si debe usar la posición del efecto de preparación
    /// </summary>
    private bool ShouldUsePreparationPosition(SpellCastContext context)
    {
        return context.commandUsed == SpellCommandType.DIRECTIONAL && preparationEffect != null;
    }

    /// <summary>
    /// Actualiza el contexto con información del objetivo
    /// </summary>
    private void UpdateContextWithTarget(ref SpellCastContext context, Vector3 targetPosition)
    {
        Transform origin = raycastOrigin ?? spellSpawnPoint;
        Transform rotationSource = (useRaycastOriginRotation && raycastOrigin != null) ? raycastOrigin : spellSpawnPoint;
        Vector3 rayDirection = useUpwardRaycast ? rotationSource.up : rotationSource.forward;

        if (Physics.Raycast(origin.position, rayDirection, out RaycastHit hitInfo, maxRaycastDistance, raycastLayers))
        {
            context.targetPosition = hitInfo.point;
            context.targetNormal = hitInfo.normal;
            context.hasValidTarget = true;
        }
        else
        {
            context.targetPosition = origin.position + rayDirection * maxRaycastDistance;
            context.targetNormal = Vector3.up;
            context.hasValidTarget = false;
        }
    }

    /// <summary>
    /// Limpia efectos antes de ejecutar el hechizo
    /// </summary>
    private void CleanupPreExecutionEffects()
    {
        ClearPreparationEffect();
        StartCoroutine(FadeOutCircles());
    }

    /// <summary>
    /// Resetea el estado después de lanzar el hechizo
    /// </summary>
    private void ResetSpellState()
    {
        chargingController = null;
        chargeComplete = false;
        SetSpellState(SpellState.IDLE);
    }

    /// <summary>
    /// Crea un contexto modificado usando la posición del efecto de preparación
    /// </summary>
    private SpellCastContext CreateModifiedContextForPreparationEffect(SpellCastContext originalContext)
    {
        var modifiedContext = originalContext;

        // Crear transform temporal con posición del efecto y rotación del bastón
        var tempOrigin = new GameObject("TempSpellOrigin");
        tempOrigin.transform.position = preparationEffect.transform.position;
        tempOrigin.transform.rotation = spellSpawnPoint.rotation;

        modifiedContext.staffTransform = tempOrigin.transform;

        // Destruir después de un frame
        StartCoroutine(DestroyAfterFrame(tempOrigin));

        return modifiedContext;
    }

    /// <summary>
    /// Destruye un GameObject después de un frame
    /// </summary>
    private IEnumerator DestroyAfterFrame(GameObject obj)
    {
        yield return null;
        if (obj != null) Destroy(obj);
    }

    /// <summary>
    /// Resetea el estado del sistema de escalado
    /// </summary>
    private void ResetScalingState()
    {
        isScalingActive = false;
        isScalingButtonPressed = false;
        currentSpellScale = 1f;
        targetSpellScale = 1f;
        nonDominantController = null;
    }

    #endregion
    #region Sistema de Escalado

    /// <summary>
    /// Se ejecuta cuando se presiona el botón de escalado
    /// </summary>
    private void OnScalingActivationStarted(InputAction.CallbackContext context)
    {
        // Validar condiciones para escalado
        if (!CanActivateScaling()) return;

        // Buscar controlador no dominante
        nonDominantController = FindNonDominantController();

        if (nonDominantController != null)
        {
            isScalingButtonPressed = true;
            isScalingActive = true;

            if (showScalingDebug)
                Debug.Log($"[MagicStaff] Escalado activado con: {nonDominantController.name}");
        }
    }

    /// <summary>
    /// Se ejecuta cuando se suelta el botón de escalado
    /// </summary>
    private void OnScalingActivationEnded(InputAction.CallbackContext context)
    {
        if (!isScalingActive) return;

        if (showScalingDebug)
            Debug.Log($"[MagicStaff] Escalado finalizado: {currentSpellScale:F2}x");

        DeactivateScaling();
    }

    /// <summary>
    /// Actualiza el sistema de escalado (llamado desde Update)
    /// </summary>
    private void UpdateScaling()
    {
        if (!isScalingActive || nonDominantController == null || !equippedSpell.AllowScaling)
            return;

        // Calcular y aplicar nueva escala
        float newTargetScale = CalculateScaleFromDistance();

        // Suavizar transición
        targetSpellScale = newTargetScale;
        currentSpellScale = Mathf.Lerp(currentSpellScale, targetSpellScale,
                                      Time.deltaTime / equippedSpell.ScalingSmoothingFactor);

        // Aplicar escalado visual
        ApplyVisualScaling();
    }

    // ─── Métodos de Cálculo ───

    /// <summary>
    /// Calcula la escala basada en la distancia de la mano
    /// </summary>
    private float CalculateScaleFromDistance()
    {
        if (scalingReferencePoint == null || nonDominantController == null)
            return 1f;

        float distance = GetDistanceToNonDominantHand();

        // Mapear distancia a escala
        float normalizedDistance = Mathf.InverseLerp(minScalingDistance, maxScalingDistance, distance);
        float rawScale = Mathf.Lerp(equippedSpell.MinScale, equippedSpell.MaxScale, normalizedDistance);

        return Mathf.Clamp(rawScale, equippedSpell.MinScale, equippedSpell.MaxScale);
    }

    /// <summary>
    /// Obtiene la distancia a la mano no dominante
    /// </summary>
    private float GetDistanceToNonDominantHand()
    {
        if (scalingReferencePoint == null || nonDominantController == null)
            return baseScalingDistance;

        return Vector3.Distance(scalingReferencePoint.position, nonDominantController.transform.position);
    }

    // ─── Métodos de Búsqueda ───

    /// <summary>
    /// Encuentra el controlador no dominante disponible
    /// </summary>
    private XRBaseController FindNonDominantController()
    {
        UpdateControllerCache();

        foreach (var controller in allControllers)
        {
            if (controller == null) continue;

            var spellController = controller.GetComponent<SpellCastController>();
            if (spellController != null && !spellController.IsDominantHand && !IsHeldBy(controller))
            {
                return controller;
            }
        }

        return null;
    }

    /// <summary>
    /// Actualiza la caché de controladores disponibles
    /// </summary>
    private void UpdateControllerCache()
    {
        if (Time.time - lastControllerCacheUpdate < CONTROLLER_CACHE_LIFETIME) return;

        allControllers.Clear();

        var controllers = FindObjectsOfType<SpellCastController>();
        foreach (var controller in controllers)
        {
            if (controller.Controller != null)
                allControllers.Add(controller.Controller);
        }

        lastControllerCacheUpdate = Time.time;
    }

    // ─── Métodos de Aplicación Visual ───

    /// <summary>
    /// Aplica el escalado visual a todos los elementos
    /// </summary>
    private void ApplyVisualScaling()
    {
        // Escalar efecto de preparación
        if (preparationEffect != null)
        {
            preparationEffect.transform.localScale = Vector3.one * currentSpellScale;
            ApplyParticleScalingStrategy();
        }

        // Escalar círculos mágicos
        foreach (var effect in activeEffects)
        {
            if (effect != null)
                effect.transform.localScale = Vector3.one * currentSpellScale;
        }
    }

    /// <summary>
    /// Aplica la estrategia de escalado a las partículas
    /// </summary>
    private void ApplyParticleScalingStrategy()
    {
        if (preparationEffect == null) return;

        var particles = preparationEffect.GetComponentsInChildren<ParticleSystem>();

        foreach (var ps in particles)
        {
            // Cachear propiedades originales si no existen
            if (!originalParticleProperties.ContainsKey(ps))
            {
                originalParticleProperties[ps] = new ParticleSystemOriginalProperties(ps);
            }

            // Aplicar estrategia según configuración
            ApplyStrategyToParticle(ps, currentSpellScale);
        }
    }

    /// <summary>
    /// Aplica la estrategia específica a un sistema de partículas
    /// </summary>
    private void ApplyStrategyToParticle(ParticleSystem ps, float scale)
    {
        if (!originalParticleProperties.TryGetValue(ps, out var original)) return;

        var main = ps.main;
        var shape = ps.shape;
        var emission = ps.emission;

        switch (particleScalingStrategy)
        {
            case ParticleScalingStrategy.NO_SCALING:
                main.startSize = original.startSize;
                shape.radius = original.shapeRadius;
                shape.scale = original.shapeBox;
                break;

            case ParticleScalingStrategy.SOFT_SCALING:
                float softScale = Mathf.Sqrt(scale);
                main.startSize = original.startSize * softScale;
                shape.radius = original.shapeRadius * softScale;
                shape.scale = original.shapeBox * softScale;
                break;

            case ParticleScalingStrategy.LIMITED_SCALING:
                float limitedScale = Mathf.Min(scale, maxParticleScaleFactor);
                main.startSize = original.startSize * limitedScale;
                shape.radius = original.shapeRadius * limitedScale;
                shape.scale = original.shapeBox * limitedScale;
                break;

            case ParticleScalingStrategy.FULL_SCALING:
                main.startSize = original.startSize * scale;
                shape.radius = original.shapeRadius * scale;
                shape.scale = original.shapeBox * scale;
                break;

            case ParticleScalingStrategy.EMISSION_ONLY:
                emission.rateOverTime = original.emissionRate * scale;
                break;
        }
    }

    // ─── Métodos de Validación ───

    /// <summary>
    /// Verifica si se puede activar el escalado
    /// </summary>
    private bool CanActivateScaling()
    {
        bool canScale = currentState == SpellState.CHARGING &&
                       preparationEffect != null &&
                       equippedSpell != null &&
                       equippedSpell.AllowScaling;

        if (showScalingDebug && !canScale)
        {
            var reason = GetScalingRejectionReason();
            Debug.Log($"[MagicStaff] Escalado rechazado: {reason}");
        }

        return canScale;
    }

    /// <summary>
    /// Obtiene la razón por la que no se puede escalar
    /// </summary>
    private string GetScalingRejectionReason()
    {
        if (currentState != SpellState.CHARGING) return "Estado incorrecto";
        if (preparationEffect == null) return "Sin efecto de preparación";
        if (equippedSpell == null) return "Sin hechizo equipado";
        if (!equippedSpell.AllowScaling) return "Hechizo no escalable";
        return "Razón desconocida";
    }

    #endregion
    #region Update y Timeouts

    private void Update()
    {
        // Actualizar progreso de carga si está activo
        if (isCharging && equippedSpell != null)
        {
            UpdateChargingProgress();
        }

        // Actualizar escalado solo durante carga con efecto visible
        if (currentState == SpellState.CHARGING && preparationEffect != null)
        {
            UpdateScaling();
        }

        // Actualizar sistemas según estado actual
        if (currentState == SpellState.PREPARED)
        {
            UpdatePreparationState();
            UpdateTargeting();
            UpdateGestureDetection();
        }
    }

    /// <summary>
    /// Actualiza el progreso de carga del hechizo
    /// </summary>
    private void UpdateChargingProgress()
    {
        float chargeTime = Time.time - chargeStartTime;
        float progress = Mathf.Clamp01(chargeTime / equippedSpell.MinChargeTime);

        // Actualizar efectos visuales solo si el progreso cambió significativamente
        if (Mathf.Abs(lastProgress - progress) > 0.01f)
        {
            lastProgress = progress;
            UpdateCircleProgress(progress);
        }

        // Verificar carga completa
        if (progress >= 1.0f && !chargeComplete)
        {
            OnChargeCompleted();
        }
    }

    /// <summary>
    /// Actualiza el estado de preparación y verifica timeouts
    /// </summary>
    private void UpdatePreparationState()
    {
        if (equippedSpell.CommandTimeout <= 0) return;

        float preparationTime = Time.time - preparationStartTime;

        if (preparationTime > equippedSpell.CommandTimeout)
        {
            HandlePreparationTimeout();
        }
    }

    // ─── Métodos de Manejo de Estados ───

    /// <summary>
    /// Se ejecuta cuando la carga se completa
    /// </summary>
    private void OnChargeCompleted()
    {
        chargeComplete = true;

        // Mostrar efecto de preparación si el hechizo lo requiere
        if (equippedSpell.RequiresPreparation)
        {
            ShowPreparationEffect();
        }
    }

    /// <summary>
    /// Maneja el timeout de preparación según el tipo de comando
    /// </summary>
    private void HandlePreparationTimeout()
    {
        if (showCommandDebug)
            Debug.Log($"[MagicStaff] Timeout alcanzado - Ejecutando acción por defecto");

        switch (equippedSpell.CommandType)
        {
            case SpellCommandType.DIRECTIONAL:
                ExecuteDirectionalTimeoutSpell();
                break;

            case SpellCommandType.EMERGE:
                ExecuteGestureTimeoutSpell(SpellCommandType.EMERGE, Vector3.up);
                break;

            case SpellCommandType.DESCEND:
                ExecuteGestureTimeoutSpell(SpellCommandType.DESCEND, Vector3.down);
                break;

            case SpellCommandType.INSTANT:
                ExecuteSpellDirectly();
                break;

            default:
                HandleDefaultTimeout();
                break;
        }
    }

    /// <summary>
    /// Maneja el timeout por defecto
    /// </summary>
    private void HandleDefaultTimeout()
    {
        if (equippedSpell.AllowInstantFallback)
        {
            currentContext.commandUsed = SpellCommandType.INSTANT;
            ExecuteSpellWithContext(currentContext);
        }
        else
        {
            CancelPreparation();
        }
    }

    // ─── Métodos de Timeout Específicos ───

    /// <summary>
    /// Ejecuta un hechizo direccional por timeout
    /// </summary>
    private void ExecuteDirectionalTimeoutSpell()
    {
        // Obtener posición actual del objetivo
        GetCurrentTargetPosition(out Vector3 targetPosition);

        // Configurar contexto con intensidad reducida
        currentContext.targetPosition = targetPosition;
        currentContext.hasValidTarget = true;
        currentContext.commandUsed = SpellCommandType.DIRECTIONAL;
        currentContext.commandDirection = (targetPosition - spellSpawnPoint.position).normalized;
        currentContext.commandIntensity = 0.5f; // Penalización por timeout

        ExecuteSpellWithContext(currentContext);
    }

    /// <summary>
    /// Ejecuta un hechizo de gesto por timeout
    /// </summary>
    private void ExecuteGestureTimeoutSpell(SpellCommandType gestureType, Vector3 direction)
    {
        // Configurar contexto con gesto simulado
        currentContext.commandUsed = gestureType;
        currentContext.commandDirection = direction;
        currentContext.commandIntensity = 0.7f; // Intensidad moderada

        ExecuteSpellWithContext(currentContext);
    }

    // ─── Métodos Auxiliares ───

    /// <summary>
    /// Actualiza el progreso visual de los círculos mágicos
    /// </summary>
    private void UpdateCircleProgress(float progress)
    {
        foreach (var effect in activeEffects)
        {
            if (effect != null)
                effect.UpdateProgress(progress);
        }
    }

    #endregion
    #region Efectos Visuales y Sonoros

    /// <summary>
    /// Reproduce efectos de lanzamiento del hechizo
    /// </summary>
    private void PlayCastingEffects()
    {
        // Activar partículas de lanzamiento
        if (castingVFX != null && !castingVFX.isPlaying)
        {
            castingVFX.Play();
        }

        // Reproducir sonido de lanzamiento
        PlaySound(castSoundId);
    }

    /// <summary>
    /// Activa o desactiva efectos de carga
    /// </summary>
    private void PlayChargingEffects(bool activate)
    {
        if (chargingVFX == null) return;

        if (activate)
        {
            if (!chargingVFX.isPlaying)
            {
                chargingVFX.Play();
                PlaySound(chargingSoundId);
            }
        }
        else
        {
            if (chargingVFX.isPlaying)
            {
                chargingVFX.Stop();
                StopSound(chargingSoundId);
            }
        }
    }

    /// <summary>
    /// Reproduce efectos de cancelación
    /// </summary>
    private void PlayCancelEffects()
    {
        // Activar partículas de cancelación
        if (cancelVFX != null)
        {
            cancelVFX.Play();
        }

        // Reproducir sonido de cancelación
        PlaySound(cancelSoundId);
    }

    /// <summary>
    /// Detiene todos los efectos de partículas
    /// </summary>
    private void StopAllParticleEffects()
    {
        StopParticleSystem(castingVFX);
        StopParticleSystem(chargingVFX);
        StopParticleSystem(cancelVFX);

        // Detener todos los sonidos
        if (audioController != null)
        {
            audioController.StopAllSounds(false);
        }
    }

    // ─── Gestión de Círculos Mágicos ───

    /// <summary>
    /// Crea un círculo mágico con retraso
    /// </summary>
    private IEnumerator CreateDelayedCircle(SpellBase.MagicCircleConfig config)
    {
        // Validar que seguimos cargando
        if (!isCharging) yield break;

        // Esperar el retraso configurado
        if (config.appearDelay > 0)
        {
            yield return new WaitForSeconds(config.appearDelay);
            if (!isCharging) yield break;
        }

        // Calcular posición y crear círculo
        Vector3 position = spellSpawnPoint.position +
                          spellSpawnPoint.TransformDirection(config.positionOffset);

        GameObject circle = Instantiate(config.circlePrefab, position, spellSpawnPoint.rotation, spellSpawnPoint);

        // Configurar efecto si existe
        var effect = circle.GetComponent<VFXCircleEffect>();
        if (effect != null)
        {
            effect.isDecorative = false;
            effect.InitializeWithDelay(config.appearDelay, equippedSpell.MinChargeTime);
            effect.StartChargeEffect();
            activeEffects.Add(effect);
        }

        activeCircles.Add(circle);
    }

    /// <summary>
    /// Desvanece y limpia todos los círculos mágicos
    /// </summary>
    private IEnumerator FadeOutCircles()
    {
        // Iniciar desvanecimiento
        foreach (var effect in activeEffects)
        {
            if (effect != null)
                effect.Hide();
        }

        // Esperar a que terminen las animaciones
        yield return new WaitForSeconds(0.5f);

        // Limpiar círculos
        ClearAllCircles();
    }

    /// <summary>
    /// Limpia todos los círculos mágicos activos
    /// </summary>
    private void ClearAllCircles()
    {
        // Destruir objetos
        foreach (var circle in activeCircles)
        {
            if (circle != null)
                Destroy(circle);
        }

        // Limpiar listas
        activeCircles.Clear();
        activeEffects.Clear();
    }

    /// <summary>
    /// Limpia el efecto de preparación y su caché
    /// </summary>
    private void ClearPreparationEffect()
    {
        if (preparationEffect != null)
        {
            // Limpiar caché de partículas
            originalParticleProperties.Clear();

            // Destruir efecto
            Destroy(preparationEffect);
            preparationEffect = null;
        }
    }

    // ─── Métodos de Feedback ───

    /// <summary>
    /// Reproduce feedback de lanzamiento fallido
    /// </summary>
    private void PlayFailedCastFeedback()
    {
        // TODO: Implementar efectos visuales/sonoros específicos
        if (showDebugMessages)
            Debug.Log("[MagicStaff] ¡No hay hechizo equipado!");
    }

    /// <summary>
    /// Reproduce feedback de mana insuficiente
    /// </summary>
    private void PlayInsufficientManaFeedback()
    {
        // TODO: Implementar efectos visuales/sonoros específicos
        if (showDebugMessages)
            Debug.Log("[MagicStaff] ¡Mana insuficiente!");
    }

    /// <summary>
    /// Reproduce feedback de cooldown activo
    /// </summary>
    private void PlayCooldownFeedback()
    {
        // TODO: Implementar efectos visuales/sonoros específicos
        if (showDebugMessages)
            Debug.Log("[MagicStaff] ¡Hechizo en cooldown!");
    }

    // ─── Métodos Auxiliares ───

    /// <summary>
    /// Reproduce un sonido a través del controlador de audio
    /// </summary>
    private void PlaySound(string soundId)
    {
        if (audioController != null && !string.IsNullOrEmpty(soundId))
        {
            audioController.Play(soundId);
        }
    }

    /// <summary>
    /// Detiene un sonido específico
    /// </summary>
    private void StopSound(string soundId)
    {
        if (audioController != null && !string.IsNullOrEmpty(soundId))
        {
            audioController.StopSound(soundId, true);
        }
    }

    /// <summary>
    /// Detiene un sistema de partículas si está activo
    /// </summary>
    private void StopParticleSystem(ParticleSystem ps)
    {
        if (ps != null && ps.isPlaying)
        {
            ps.Stop();
        }
    }

    #endregion
    #region Depuración

#if UNITY_EDITOR
    /// <summary>
    /// Muestra información de debug en pantalla (solo en editor)
    /// </summary>
    private void OnGUI()
    {
        if (!showDebugMessages) return;

        var style = new GUIStyle(GUI.skin.label) { fontSize = 12 };
        int y = 10;
        const int lineHeight = 20;
        const int x = 10;

        // Información básica
        DrawDebugSection(ref y, "━━━ ESTADO GENERAL ━━━", style);
        DrawDebugLine(ref y, $"Estado: {currentState}", style);
        DrawDebugLine(ref y, $"Cargando: {isCharging}", style);

        if (equippedSpell != null)
        {
            DrawDebugLine(ref y, $"Hechizo: {equippedSpell.SpellName}", style);
            DrawDebugLine(ref y, $"Cooldown: {equippedSpell.RemainingCooldown:F1}s", style);
        }

        // Información de escalado
        if (showScalingDebug && equippedSpell != null && equippedSpell.AllowScaling)
        {
            y += 10;
            DrawDebugSection(ref y, "━━━ ESCALADO ━━━", style);
            DrawDebugLine(ref y, $"Activo: {isScalingActive}", style);
            DrawDebugLine(ref y, $"Escala: {currentSpellScale:F2}x", style);

            if (isScalingActive)
            {
                DrawScalingDetails(ref y, style);
            }
        }

        // Información de comandos
        if (showCommandDebug && currentState == SpellState.PREPARED)
        {
            y += 10;
            DrawDebugSection(ref y, "━━━ COMANDO ━━━", style);
            DrawDebugLine(ref y, $"Tipo: {equippedSpell.CommandType}", style);

            if (equippedSpell.CommandTimeout > 0)
            {
                float remaining = equippedSpell.CommandTimeout - (Time.time - preparationStartTime);
                DrawDebugLine(ref y, $"Tiempo: {remaining:F1}s", style);
            }

            DrawCommandSpecificInfo(ref y, style);
        }
    }

    /// <summary>
    /// Dibuja información específica de escalado
    /// </summary>
    private void DrawScalingDetails(ref int y, GUIStyle style)
    {
        if (nonDominantController != null)
        {
            float distance = GetDistanceToNonDominantHand();
            DrawDebugLine(ref y, $"Distancia: {distance:F2}m", style);
        }

        string efficiency = equippedSpell.GetScalingEfficiencyDescription(currentSpellScale);
        var color = equippedSpell.GetScalingEfficiencyColor(currentSpellScale);

        GUI.color = color;
        DrawDebugLine(ref y, $"Eficiencia: {efficiency}", style);
        GUI.color = Color.white;

        if (playerStats != null)
        {
            var result = equippedSpell.CalculateScaledProperties(currentSpellScale, playerStats.Mana.GetCurrentValue());
            DrawDebugLine(ref y, $"Coste mana: {result.finalManaCost:F1}", style);

            if (!result.isViable)
            {
                GUI.color = Color.red;
                DrawDebugLine(ref y, "¡MANA INSUFICIENTE!", style);
                GUI.color = Color.white;
            }
        }
    }

    /// <summary>
    /// Dibuja información específica del comando actual
    /// </summary>
    private void DrawCommandSpecificInfo(ref int y, GUIStyle style)
    {
        switch (equippedSpell.CommandType)
        {
            case SpellCommandType.DIRECTIONAL:
                DrawDebugLine(ref y, $"Apuntando: {isTargeting}", style);
                if (isTargeting)
                {
                    float progress = (Time.time - targetingStartTime) / equippedSpell.AimHoldTime;
                    DrawDebugLine(ref y, $"Progreso: {progress:P0}", style);
                }
                break;

            case SpellCommandType.EMERGE:
                DrawDebugLine(ref y, "Esperando: Gesto ARRIBA", style);
                break;

            case SpellCommandType.DESCEND:
                DrawDebugLine(ref y, "Esperando: Gesto ABAJO", style);
                break;
        }
    }

    /// <summary>
    /// Dibuja una línea de debug
    /// </summary>
    private void DrawDebugLine(ref int y, string text, GUIStyle style)
    {
        GUI.Label(new Rect(10, y, 500, 20), text, style);
        y += 20;
    }

    /// <summary>
    /// Dibuja un encabezado de sección
    /// </summary>
    private void DrawDebugSection(ref int y, string title, GUIStyle style)
    {
        GUI.Label(new Rect(10, y, 500, 20), title, style);
        y += 20;
    }
#endif

    /// <summary>
    /// Dibuja gizmos en el editor para visualización
    /// </summary>
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        // Visualización de raycast/targeting
        if (showTargetingDebug)
        {
            DrawTargetingGizmos();
        }

        // Visualización de escalado
        if (showScalingDebug && scalingReferencePoint != null)
        {
            DrawScalingGizmos();
        }
    }

    /// <summary>
    /// Dibuja gizmos del sistema de targeting
    /// </summary>
    private void DrawTargetingGizmos()
    {
        Transform origin = raycastOrigin ?? spellSpawnPoint;
        if (origin == null) return;

        Transform rotationSource = (useRaycastOriginRotation && raycastOrigin != null) ? raycastOrigin : spellSpawnPoint;
        if (rotationSource == null) return;

        Vector3 rayDirection = useUpwardRaycast ? rotationSource.up : rotationSource.forward;

        // Dibujar raycast
        if (Physics.Raycast(origin.position, rayDirection, out RaycastHit hit, maxRaycastDistance, raycastLayers))
        {
            // Línea hasta el punto de impacto
            Gizmos.color = Color.green;
            Gizmos.DrawLine(origin.position, hit.point);

            // Punto de impacto
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(hit.point, 0.1f);

            // Normal de la superficie
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(hit.point, hit.point + hit.normal * 0.5f);

            // Área de tolerancia si está apuntando
            if (currentState == SpellState.PREPARED &&
                equippedSpell?.CommandType == SpellCommandType.DIRECTIONAL &&
                isTargeting)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(hit.point, equippedSpell.AimTolerance);
            }
        }
        else
        {
            // Línea hasta distancia máxima
            Gizmos.color = Color.gray;
            Gizmos.DrawLine(origin.position, origin.position + rayDirection * maxRaycastDistance);
        }

        // Origen del raycast
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(origin.position, 0.05f);
    }

    /// <summary>
    /// Dibuja gizmos del sistema de escalado
    /// </summary>
    private void DrawScalingGizmos()
    {
        Vector3 refPos = scalingReferencePoint.position;

        // Esferas de distancia
        DrawDistanceSphere(refPos, minScalingDistance, Color.red, "Mín");
        DrawDistanceSphere(refPos, baseScalingDistance, Color.yellow, "Base");
        DrawDistanceSphere(refPos, maxScalingDistance, Color.green, "Máx");

        // Conexión con mano no dominante si está activa
        if (isScalingActive && nonDominantController != null)
        {
            Vector3 handPos = nonDominantController.transform.position;

            // Línea de conexión
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(refPos, handPos);

            // Posición de la mano
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(handPos, 0.02f);

            // Visualización de escala actual
            Color scaleColor = equippedSpell != null ?
                equippedSpell.GetScalingEfficiencyColor(currentSpellScale) : Color.white;
            Gizmos.color = scaleColor;
            Gizmos.DrawWireCube(refPos, Vector3.one * currentSpellScale * 0.1f);
        }
    }

    /// <summary>
    /// Dibuja una esfera de distancia con etiqueta
    /// </summary>
    private void DrawDistanceSphere(Vector3 center, float radius, Color color, string label)
    {
        Gizmos.color = color;
        Gizmos.DrawWireSphere(center, radius);

#if UNITY_EDITOR
        UnityEditor.Handles.color = color;
        UnityEditor.Handles.Label(center + Vector3.up * radius, label);
#endif
    }

    #endregion
    #region Estructuras y Tipos Auxiliares

    /// <summary>
    /// Estructura para guardar las propiedades originales de un sistema de partículas
    /// </summary>
    [System.Serializable]
    private struct ParticleSystemOriginalProperties
    {
        public float startSize;
        public float startSpeed;
        public float emissionRate;
        public float shapeRadius;
        public Vector3 shapeBox;

        /// <summary>
        /// Constructor que captura las propiedades actuales del sistema
        /// </summary>
        public ParticleSystemOriginalProperties(ParticleSystem ps)
        {
            var main = ps.main;
            var emission = ps.emission;
            var shape = ps.shape;

            startSize = main.startSize.constant;
            startSpeed = main.startSpeed.constant;
            emissionRate = emission.rateOverTime.constant;
            shapeRadius = shape.radius;
            shapeBox = shape.scale;
        }
    }

    #endregion
}