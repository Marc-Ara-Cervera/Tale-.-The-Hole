using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using System;
using System.Collections.Generic;
using static SpellBase;
using System.Collections;

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

public class MagicStaff : MonoBehaviour
{
    #region Variables y Referencias

    [Header("Configuración del Bastón")]
    [SerializeField] private Transform spellSpawnPoint;
    [SerializeField] private ParticleSystem castingVFX;

    [Header("Hechizo")]
    [SerializeField] private SpellBase equippedSpell;

    [Header("Sistema de Carga")]
    [SerializeField] private ParticleSystem chargingVFX;
    [SerializeField] private ParticleSystem cancelVFX;

    [Header("Audio")]
    [SerializeField] private AudioController audioController;
    [SerializeField] private string chargingSoundId = "staff_charging";
    [SerializeField] private string castSoundId = "staff_cast";
    [SerializeField] private string cancelSoundId = "staff_cancel";

    [Header("Sistema de Targeting")]
    [SerializeField] private LayerMask raycastLayers = -1;
    [SerializeField] private float maxRaycastDistance = 50f;
    [SerializeField] private bool showTargetingDebug = true;

    [Tooltip("Si está marcado, el raycast va hacia arriba. Si no, va hacia adelante")]
    [SerializeField] private bool useUpwardRaycast = false;
    [Header("Configuración de Raycast")]
    [Tooltip("Transform desde donde sale el raycast. Si está vacío, usa spellSpawnPoint")]
    [SerializeField] private Transform raycastOrigin;
    [Tooltip("Si está marcado, usa la rotación del raycastOrigin para la dirección. Si no, usa spellSpawnPoint")]
    [SerializeField] private bool useRaycastOriginRotation = true;

    [Header("Sistema de Comandos")]
    [SerializeField] private bool showCommandDebug = true;
    [Tooltip("Velocidad mínima para detectar gestos hacia arriba/abajo")]
    [SerializeField] private float gestureMinSpeed = 2f;
    [Tooltip("Tiempo mínimo entre gestos para evitar detecciones múltiples")]
    [SerializeField] private float gestureCooldown = 0.5f;

    // Referencias a componentes
    private XRGrabInteractable grabInteractable;

    // Lista de todos los controladores que actualmente están agarrando este bastón
    private List<XRBaseController> heldByControllers = new List<XRBaseController>();

    // Referencia a las estadísticas del jugador
    private PlayerStatsManager playerStats;

    // NUEVO: Estado del sistema de hechizos
    private SpellState currentState = SpellState.IDLE;

    // Estado de carga (EXISTENTE)
    private bool isCharging = false;
    private XRBaseController chargingController = null;
    private float chargeStartTime;
    private bool chargeComplete;
    private float lastProgress = -1f;

    // NUEVO: Estado de preparación
    private GameObject preparationEffect = null;
    private float preparationStartTime;
    private SpellCastContext currentContext;

    // NUEVO: Sistema de targeting direccional
    private Vector3 lastTargetPosition;
    private float targetingStartTime;
    private bool isTargeting = false;

    // NUEVO: Sistema de detección de gestos
    private Vector3 lastControllerPosition;
    private Vector3 lastControllerVelocity;
    private float lastGestureTime = -999f;

    // Para depuración
    [SerializeField] private bool showDebugMessages = true;

    // Lista para seguir los círculos activos
    private List<GameObject> activeCircles = new List<GameObject>();
    private List<VFXCircleEffect> activeEffects = new List<VFXCircleEffect>();

    // Eventos
    public event Action<SpellBase> OnSpellCast;
    public event Action<bool> OnSpellChargeStateChanged;
    public event Action<SpellState> OnSpellStateChanged; // NUEVO

    #endregion

    #region Inicialización y Configuración

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();

        if (spellSpawnPoint == null)
        {
            spellSpawnPoint = transform;
            Debug.LogWarning("No se asignó un punto de generación de hechizos. Usando la posición del bastón.");
        }

        if (grabInteractable == null)
        {
            Debug.LogError("Missing XRGrabInteractable component on magic staff!");
            return;
        }
    }

    private void OnEnable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.AddListener(OnGrabbed);
            grabInteractable.selectExited.AddListener(OnReleased);
        }
    }

    private void OnDisable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnGrabbed);
            grabInteractable.selectExited.RemoveListener(OnReleased);
        }

        // Limpiar estado al desactivar
        ResetAllState();
    }

    /// <summary>
    /// NUEVO: Resetea completamente el estado del bastón
    /// </summary>
    private void ResetAllState()
    {
        SetSpellState(SpellState.IDLE);
        isCharging = false;
        chargingController = null;
        heldByControllers.Clear();
        isTargeting = false;

        StopAllParticleEffects();
        ClearAllCircles();
        ClearPreparationEffect();
    }

    #endregion

    #region Gestión de Interacción VR (Sin cambios significativos)

    /// <summary>
    /// Se llama cuando el bastón es agarrado por un controlador VR
    /// </summary>
    public void OnGrabbed(SelectEnterEventArgs args)
    {
        XRBaseController newController = args.interactorObject.transform.GetComponent<XRBaseController>();
        if (newController == null)
            return;

        if (showDebugMessages)
        {
            Debug.Log($"[{Time.frameCount}] Bastón agarrado por: {newController.name}");
        }

        if (heldByControllers.Contains(newController))
            return;

        heldByControllers.Add(newController);

        SpellCastController spellController = newController.GetComponent<SpellCastController>();
        if (spellController != null && spellController.IsDominantHand)
        {
            playerStats = newController.transform.root.GetComponent<PlayerStatsManager>();

            if (showDebugMessages)
            {
                Debug.Log($"[{Time.frameCount}] Controlador dominante {newController.name} registrado, playerStats asignado");
            }
        }
    }

    /// <summary>
    /// Se llama cuando el bastón es soltado
    /// </summary>
    public void OnReleased(SelectExitEventArgs args)
    {
        XRBaseController releasedController = args.interactorObject.transform.GetComponent<XRBaseController>();
        if (releasedController == null)
            return;

        if (showDebugMessages)
        {
            Debug.Log($"[{Time.frameCount}] Bastón soltado por: {releasedController.name}");
        }

        heldByControllers.Remove(releasedController);

        // Si estábamos cargando o en cualquier estado activo con este controlador, cancelar
        if ((isCharging || currentState != SpellState.IDLE) && releasedController == chargingController)
        {
            if (showDebugMessages)
            {
                Debug.Log($"[{Time.frameCount}] Cancelando actividad porque el controlador activo soltó el bastón");
            }
            CancelCurrentSpell();
        }

        // Actualizar playerStats si es necesario
        if (heldByControllers.Count == 0)
        {
            playerStats = null;
        }
        else
        {
            foreach (XRBaseController controller in heldByControllers)
            {
                SpellCastController spellController = controller.GetComponent<SpellCastController>();
                if (spellController != null && spellController.IsDominantHand)
                {
                    playerStats = controller.transform.root.GetComponent<PlayerStatsManager>();
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Verifica si este bastón está siendo sostenido por un controlador específico
    /// </summary>
    public bool IsHeldBy(XRBaseController controller)
    {
        return heldByControllers.Contains(controller);
    }

    /// <summary>
    /// Verifica si este bastón está siendo sostenido por un controlador dominante
    /// </summary>
    public bool IsHeldByDominantHand(XRBaseController controller)
    {
        if (!heldByControllers.Contains(controller))
            return false;

        SpellCastController spellController = controller.GetComponent<SpellCastController>();
        return spellController != null && spellController.IsDominantHand;
    }

    #endregion

    #region Sistema de Estados - NUEVO

    /// <summary>
    /// Cambia el estado actual del sistema de hechizos
    /// </summary>
    private void SetSpellState(SpellState newState)
    {
        if (currentState == newState) return;

        SpellState oldState = currentState;
        currentState = newState;

        if (showCommandDebug)
        {
            Debug.Log($"[{Time.frameCount}] Estado cambiado: {oldState} → {newState}");
        }

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

    #endregion

    #region Sistema de Carga (MODIFICADO)

    /// <summary>
    /// Comienza la carga de un hechizo
    /// </summary>
    public void StartCharging(XRBaseController requestingController)
    {
        if (!IsHeldByDominantHand(requestingController))
        {
            if (showDebugMessages)
            {
                Debug.Log($"[{Time.frameCount}] Ignorando StartCharging de {requestingController.name} - No es dominante o no sostiene el bastón");
            }
            return;
        }

        if (equippedSpell == null)
        {
            PlayFailedCastFeedback();
            return;
        }

        if (playerStats == null || !playerStats.Mana.CanCastSpell(equippedSpell.ManaCost))
        {
            PlayInsufficientManaFeedback();
            return;
        }

        // Cancelar cualquier actividad anterior
        if (currentState != SpellState.IDLE)
        {
            CancelCurrentSpell();
        }

        // Cambiar al estado de carga
        SetSpellState(SpellState.CHARGING);

        // Activar estado de carga
        isCharging = true;
        chargingController = requestingController;
        chargeStartTime = Time.time;
        chargeComplete = false;
        lastProgress = -1f;

        if (showDebugMessages)
        {
            Debug.Log($"[{Time.frameCount}] Iniciando carga de hechizo con controlador: {requestingController.name}");
        }

        // Limpiar círculos anteriores
        ClearAllCircles();

        // Crear círculos mágicos si los hay
        if (equippedSpell != null && equippedSpell.HasMagicCircles())
        {
            MagicCircleConfig[] circleConfigs = equippedSpell.GetMagicCircles();
            foreach (MagicCircleConfig config in circleConfigs)
            {
                if (config.circlePrefab != null)
                {
                    StartCoroutine(CreateDelayedCircle(config));
                }
            }
        }

        // Efectos visuales de carga
        PlayChargingEffects(true);

        // Notificar cambio de estado
        OnSpellChargeStateChanged?.Invoke(true);
    }

    /// <summary>
    /// Finaliza la carga - MODIFICADO para manejar preparación
    /// </summary>
    public void FinishCharging(XRBaseController requestingController, float chargeTime)
    {
        if (chargingController != requestingController || !isCharging)
        {
            if (showDebugMessages)
            {
                Debug.Log($"[{Time.frameCount}] Ignorando FinishCharging - controlador incorrecto o no estamos cargando");
            }
            return;
        }

        // Desactivar estado de carga
        isCharging = false;
        PlayChargingEffects(false);

        if (showDebugMessages)
        {
            Debug.Log($"[{Time.frameCount}] Finalizando carga con tiempo: {chargeTime}s");
        }

        // Verificaciones básicas
        if (equippedSpell == null)
        {
            PlayFailedCastFeedback();
            SetSpellState(SpellState.IDLE);
            return;
        }

        if (chargeTime < equippedSpell.MinChargeTime)
        {
            if (showDebugMessages)
            {
                Debug.Log($"[{Time.frameCount}] Tiempo de carga insuficiente. Requerido: {equippedSpell.MinChargeTime}s, Actual: {chargeTime}s");
            }
            PlayCancelEffects();
            SetSpellState(SpellState.IDLE);
            return;
        }

        if (playerStats == null || !playerStats.Mana.CanCastSpell(equippedSpell.ManaCost))
        {
            PlayInsufficientManaFeedback();
            SetSpellState(SpellState.IDLE);
            return;
        }

        if (!equippedSpell.IsReady())
        {
            PlayCooldownFeedback();
            SetSpellState(SpellState.IDLE);
            return;
        }

        // ===== NUEVO: Decidir si necesita preparación o ejecución inmediata =====

        if (equippedSpell.CommandType == SpellCommandType.INSTANT)
        {
            // Ejecutar inmediatamente
            ExecuteSpellDirectly();
        }
        else
        {
            // Pasar a fase de preparación
            StartPreparation();
        }
    }

    /// <summary>
    /// Cancela la carga actual
    /// </summary>
    public void CancelCharging(XRBaseController requestingController)
    {
        if (chargingController != requestingController && !IsHeldByDominantHand(requestingController))
        {
            return;
        }

        if (!isCharging) return;

        if (showDebugMessages)
        {
            Debug.Log($"[{Time.frameCount}] Cancelando carga de hechizo");
        }

        isCharging = false;
        PlayChargingEffects(false);
        PlayCancelEffects();
        StartCoroutine(FadeOutCircles());

        chargingController = null;
        chargeComplete = false;

        SetSpellState(SpellState.IDLE);
        OnSpellChargeStateChanged?.Invoke(false);
    }

    #endregion

    #region Sistema de Preparación - NUEVO

    /// <summary>
    /// Inicia la fase de preparación del hechizo
    /// </summary>
    private void StartPreparation()
    {
        SetSpellState(SpellState.PREPARED);
        preparationStartTime = Time.time;

        // Crear contexto del hechizo
        currentContext = CreateSpellContext();

        // Crear efecto de preparación si existe
        if (equippedSpell.HasPreparationPrefab)
        {
            preparationEffect = equippedSpell.CreatePreparationEffect(currentContext);
        }

        // Inicializar sistema de targeting si es necesario
        if (equippedSpell.CommandType == SpellCommandType.DIRECTIONAL)
        {
            StartTargeting();
        }

        if (showCommandDebug)
        {
            Debug.Log($"[{Time.frameCount}] Hechizo preparado. Tipo de comando: {equippedSpell.CommandType}");
        }
    }

    /// <summary>
    /// Cancela la preparación
    /// </summary>
    private void CancelPreparation()
    {
        if (currentState != SpellState.PREPARED) return;

        if (showCommandDebug)
        {
            Debug.Log($"[{Time.frameCount}] Cancelando preparación");
        }

        ClearPreparationEffect();
        StopTargeting();
        StartCoroutine(FadeOutCircles());

        SetSpellState(SpellState.IDLE);
    }

    /// <summary>
    /// Limpia el efecto de preparación
    /// </summary>
    private void ClearPreparationEffect()
    {
        if (preparationEffect != null)
        {
            Destroy(preparationEffect);
            preparationEffect = null;
        }
    }

    #endregion

    #region Sistema de Targeting Direccional - NUEVO

    /// <summary>
    /// Inicia el sistema de targeting direccional
    /// </summary>
    private void StartTargeting()
    {
        isTargeting = false; // Resetear estado
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
        if (currentState != SpellState.PREPARED) return;
        if (equippedSpell.CommandType != SpellCommandType.DIRECTIONAL) return;

        // Obtener nueva posición objetivo
        Vector3 currentTargetPosition;
        bool hasValidTarget = GetCurrentTargetPosition(out currentTargetPosition);

        if (!hasValidTarget)
        {
            // No hay objetivo válido, resetear targeting
            if (isTargeting)
            {
                isTargeting = false;
                if (showCommandDebug)
                {
                    Debug.Log($"[{Time.frameCount}] Targeting perdido - no hay objetivo válido");
                }
            }
            return;
        }

        // Verificar si el jugador está apuntando al mismo lugar
        float distance = Vector3.Distance(currentTargetPosition, lastTargetPosition);

        if (distance <= equippedSpell.AimTolerance)
        {
            // Está apuntando al mismo lugar
            if (!isTargeting)
            {
                // Comenzar targeting
                isTargeting = true;
                targetingStartTime = Time.time;
                lastTargetPosition = currentTargetPosition;

                if (showCommandDebug)
                {
                    Debug.Log($"[{Time.frameCount}] Targeting iniciado en posición: {currentTargetPosition}");
                }
            }
            else
            {
                // Verificar si ha mantenido la posición el tiempo suficiente
                float holdTime = Time.time - targetingStartTime;
                if (holdTime >= equippedSpell.AimHoldTime)
                {
                    // ¡Targeting completado!
                    ExecuteDirectionalSpell(currentTargetPosition);
                }
            }
        }
        else
        {
            // Se movió demasiado, resetear
            if (isTargeting)
            {
                isTargeting = false;
                if (showCommandDebug)
                {
                    Debug.Log($"[{Time.frameCount}] Targeting resetado - se movió demasiado (distancia: {distance})");
                }
            }
            lastTargetPosition = currentTargetPosition;
        }
    }

    /// <summary>
    /// Ejecuta un hechizo direccional con la posición objetivo
    /// </summary>
    private void ExecuteDirectionalSpell(Vector3 targetPosition)
    {
        if (showCommandDebug)
        {
            Debug.Log($"[{Time.frameCount}] Ejecutando hechizo direccional PRECISO hacia: {targetPosition}");
        }

        // Actualizar contexto con información de targeting PRECISO
        currentContext.targetPosition = targetPosition;
        currentContext.hasValidTarget = true;
        currentContext.commandUsed = SpellCommandType.DIRECTIONAL;
        currentContext.commandDirection = (targetPosition - spellSpawnPoint.position).normalized;
        currentContext.commandIntensity = 1f; // MÁXIMA intensidad porque completó el targeting preciso

        ExecuteSpellWithContext(currentContext);
    }

    #endregion

    #region Sistema de Detección de Gestos - NUEVO

    /// <summary>
    /// Actualiza la detección de gestos (llamado desde Update)
    /// </summary>
    private void UpdateGestureDetection()
    {
        if (currentState != SpellState.PREPARED) return;
        if (chargingController == null) return;

        // Solo detectar gestos para hechizos que los requieren
        SpellCommandType commandType = equippedSpell.CommandType;
        if (commandType != SpellCommandType.EMERGE && commandType != SpellCommandType.DESCEND)
            return;

        // Calcular velocidad del controlador
        Vector3 currentPosition = chargingController.transform.position;
        Vector3 currentVelocity = (currentPosition - lastControllerPosition) / Time.deltaTime;

        // Verificar cooldown entre gestos
        if (Time.time - lastGestureTime < gestureCooldown)
        {
            lastControllerPosition = currentPosition;
            lastControllerVelocity = currentVelocity;
            return;
        }

        // Detectar gesto hacia arriba (EMERGE)
        if (commandType == SpellCommandType.EMERGE)
        {
            if (currentVelocity.y > gestureMinSpeed && lastControllerVelocity.y > gestureMinSpeed)
            {
                ExecuteGestureSpell(SpellCommandType.EMERGE, Vector3.up, currentVelocity.y);
                lastGestureTime = Time.time;
            }
        }
        // Detectar gesto hacia abajo (DESCEND)
        else if (commandType == SpellCommandType.DESCEND)
        {
            if (currentVelocity.y < -gestureMinSpeed && lastControllerVelocity.y < -gestureMinSpeed)
            {
                ExecuteGestureSpell(SpellCommandType.DESCEND, Vector3.down, Mathf.Abs(currentVelocity.y));
                lastGestureTime = Time.time;
            }
        }

        // Actualizar estado anterior
        lastControllerPosition = currentPosition;
        lastControllerVelocity = currentVelocity;
    }

    /// <summary>
    /// Ejecuta un hechizo activado por gesto
    /// </summary>
    private void ExecuteGestureSpell(SpellCommandType gestureType, Vector3 direction, float intensity)
    {
        if (showCommandDebug)
        {
            Debug.Log($"[{Time.frameCount}] Gesto detectado: {gestureType} con intensidad: {intensity}");
        }

        // Actualizar contexto con información del gesto
        currentContext.commandUsed = gestureType;
        currentContext.commandDirection = direction;
        currentContext.commandIntensity = intensity;

        ExecuteSpellWithContext(currentContext);
    }

    #endregion

    #region Ejecución de Hechizos - MODIFICADO

    /// <summary>
    /// Ejecuta un hechizo inmediatamente (sin preparación)
    /// </summary>
    private void ExecuteSpellDirectly()
    {
        SetSpellState(SpellState.EXECUTING);

        // Crear contexto y ejecutar
        SpellCastContext context = CreateSpellContext();
        context.commandUsed = SpellCommandType.INSTANT;

        ExecuteSpellWithContext(context);
    }

    /// <summary>
    /// Ejecuta el hechizo con el contexto dado
    /// </summary>
    private void ExecuteSpellWithContext(SpellCastContext context)
    {
        SetSpellState(SpellState.EXECUTING);

        if (showDebugMessages)
        {
            Debug.Log($"[{Time.frameCount}] Ejecutando hechizo: {equippedSpell.name} con comando: {context.commandUsed}");
        }

        // NUEVO: Para hechizos direccionales, si hay un efecto de preparación activo,
        // modificar el contexto para que use esa posición como origen
        if (context.commandUsed == SpellCommandType.DIRECTIONAL && preparationEffect != null)
        {
            // Crear un contexto modificado que use la posición del efecto de preparación
            context = CreateModifiedContextForPreparationEffect(context);

            if (showDebugMessages)
            {
                Debug.Log($"[{Time.frameCount}] Usando posición del efecto de preparación: {preparationEffect.transform.position}");
            }
        }

        // Limpiar efectos de preparación ANTES de ejecutar
        ClearPreparationEffect();
        StartCoroutine(FadeOutCircles());

        // Consumir mana
        playerStats.Mana.CastSpell(equippedSpell.ManaCost);

        // Ejecutar el hechizo
        equippedSpell.Cast(context);

        // Efectos de lanzamiento
        PlayCastingEffects();

        // Resetear estado
        chargingController = null;
        chargeComplete = false;

        // Notificar
        OnSpellCast?.Invoke(equippedSpell);

        // Volver al estado idle
        SetSpellState(SpellState.IDLE);
    }

    /// <summary>
    /// NUEVO: Crea un contexto modificado que usa la posición del efecto de preparación
    /// como punto de origen en lugar del bastón
    /// </summary>
    private SpellCastContext CreateModifiedContextForPreparationEffect(SpellCastContext originalContext)
    {
        // Crear un nuevo contexto basado en el original
        SpellCastContext modifiedContext = originalContext;

        // Crear un transform temporal que combine la posición del efecto de preparación
        // con la rotación del bastón (para mantener la dirección de lanzamiento correcta)
        GameObject tempOrigin = new GameObject("TempSpellOrigin");
        tempOrigin.transform.position = preparationEffect.transform.position;
        tempOrigin.transform.rotation = spellSpawnPoint.rotation; // Usar rotación del bastón

        // Actualizar el contexto para usar este transform temporal
        modifiedContext.staffTransform = tempOrigin.transform;

        // Destruir el objeto temporal después de un frame (el hechizo ya habrá leído los valores)
        StartCoroutine(DestroyTempOriginAfterDelay(tempOrigin));

        return modifiedContext;
    }

    /// <summary>
    /// NUEVO: Destruye el transform temporal después de un pequeño delay
    /// </summary>
    private System.Collections.IEnumerator DestroyTempOriginAfterDelay(GameObject tempOrigin)
    {
        // Esperar un frame para que el hechizo lea los valores
        yield return null;

        if (tempOrigin != null)
        {
            Destroy(tempOrigin);
        }
    }

    /// <summary>
    /// Crea el contexto actual del hechizo
    /// </summary>
    private SpellCastContext CreateSpellContext()
    {
        SpellCastContext context = new SpellCastContext(
            spellSpawnPoint,
            playerStats.transform,
            playerStats
        );

        //  Obtener información del objetivo mediante el nuevo sistema de raycast
        Vector3 currentTargetPosition;
        if (GetCurrentTargetPosition(out currentTargetPosition))
        {
            RaycastHit hitInfo;

            // Usar el mismo origen y dirección que en GetCurrentTargetPosition
            Transform actualOrigin = raycastOrigin != null ? raycastOrigin : spellSpawnPoint;
            Vector3 rayOrigin = actualOrigin.position;

            Transform rotationSource = (useRaycastOriginRotation && raycastOrigin != null) ? raycastOrigin : spellSpawnPoint;
            Vector3 rayDirection = useUpwardRaycast ? rotationSource.up : rotationSource.forward;

            if (Physics.Raycast(rayOrigin, rayDirection, out hitInfo, maxRaycastDistance, raycastLayers))
            {
                context.targetPosition = hitInfo.point;
                context.targetNormal = hitInfo.normal;
                context.hasValidTarget = true;

                if (showTargetingDebug)
                {
                    Debug.Log($"[MagicStaff] Context created with valid target: {context.targetPosition}");
                }
            }
            else
            {
                context.targetPosition = rayOrigin + rayDirection * maxRaycastDistance;
                context.targetNormal = Vector3.up;
                context.hasValidTarget = false;
            }
        }

        return context;
    }


    /// <summary>
    /// Obtiene la posición objetivo actual basada en el raycast
    /// </summary>
    private bool GetCurrentTargetPosition(out Vector3 targetPosition)
    {
        RaycastHit hitInfo;

        // NUEVO: Usar raycastOrigin si está configurado, sino usar spellSpawnPoint
        Transform actualOrigin = raycastOrigin != null ? raycastOrigin : spellSpawnPoint;
        Vector3 rayOrigin = actualOrigin.position;

        // NUEVO: Decidir qué rotación usar para la dirección
        Transform rotationSource = (useRaycastOriginRotation && raycastOrigin != null) ? raycastOrigin : spellSpawnPoint;
        Vector3 rayDirection = useUpwardRaycast ? rotationSource.up : rotationSource.forward;

        if (Physics.Raycast(rayOrigin, rayDirection, out hitInfo, maxRaycastDistance, raycastLayers))
        {
            targetPosition = hitInfo.point;

            if (showTargetingDebug)
            {
                Debug.Log($"[MagicStaff] Raycast hit: {hitInfo.collider.name} at {hitInfo.point}");
            }

            return true;
        }

        targetPosition = rayOrigin + rayDirection * maxRaycastDistance;

        if (showTargetingDebug)
        {
            Debug.Log($"[MagicStaff] Raycast miss, using max distance: {targetPosition}");
        }

        return false;
    }

    #endregion

    #region Update y Timeouts - MODIFICADO

    private void Update()
    {
        // Actualizar carga (existente)
        if (isCharging && equippedSpell != null)
        {
            UpdateChargingProgress();
        }

        // NUEVO: Actualizar sistemas según el estado
        switch (currentState)
        {
            case SpellState.PREPARED:
                UpdatePreparationState();
                UpdateTargeting();
                UpdateGestureDetection();
                break;
        }
    }

    /// <summary>
    /// Actualiza el progreso de carga (existente, sin cambios)
    /// </summary>
    private void UpdateChargingProgress()
    {
        float chargeTime = Time.time - chargeStartTime;
        float progress = Mathf.Clamp01(chargeTime / equippedSpell.MinChargeTime);

        if (Mathf.Abs(lastProgress - progress) > 0.01f)
        {
            lastProgress = progress;

            foreach (VFXCircleEffect effect in activeEffects)
            {
                if (effect == null) continue;
                effect.UpdateProgress(progress);
            }

            if (progress >= 1.0f && !chargeComplete)
            {
                chargeComplete = true;
            }
        }
    }

    /// <summary>
    /// NUEVO: Actualiza el estado de preparación (timeouts, etc.)
    /// </summary>
    private void UpdatePreparationState()
    {
        // Verificar timeout si está configurado
        if (equippedSpell.CommandTimeout > 0)
        {
            float preparationTime = Time.time - preparationStartTime;
            if (preparationTime > equippedSpell.CommandTimeout)
            {
                if (showCommandDebug)
                {
                    Debug.Log($"[{Time.frameCount}] Timeout de preparación alcanzado");
                }

                // Comportamiento específico según el tipo de comando
                switch (equippedSpell.CommandType)
                {
                    case SpellCommandType.DIRECTIONAL:
                        // Para hechizos direccionales, disparar hacia donde apunta actualmente
                        ExecuteDirectionalTimeoutSpell();
                        break;

                    case SpellCommandType.EMERGE:
                        // Para hechizos que emergen, ejecutar automáticamente con gesto simulado hacia arriba
                        ExecuteGestureTimeoutSpell(SpellCommandType.EMERGE, Vector3.up);
                        break;

                    case SpellCommandType.DESCEND:
                        // Para hechizos que descienden, ejecutar automáticamente con gesto simulado hacia abajo
                        ExecuteGestureTimeoutSpell(SpellCommandType.DESCEND, Vector3.down);
                        break;

                    case SpellCommandType.INSTANT:
                        // Los hechizos instantáneos no deberían llegar aquí, pero por si acaso
                        currentContext.commandUsed = SpellCommandType.INSTANT;
                        ExecuteSpellWithContext(currentContext);
                        break;

                    default:
                        // Fallback general
                        if (equippedSpell.AllowInstantFallback)
                        {
                            currentContext.commandUsed = SpellCommandType.INSTANT;
                            ExecuteSpellWithContext(currentContext);
                        }
                        else
                        {
                            CancelPreparation();
                        }
                        break;
                }
            }
        }
    }

    /// <summary>
    /// NUEVO: Ejecuta un hechizo de gesto cuando se acaba el timeout
    /// Simula el gesto con intensidad por defecto
    /// </summary>
    private void ExecuteGestureTimeoutSpell(SpellCommandType gestureType, Vector3 direction)
    {
        if (showCommandDebug)
        {
            Debug.Log($"[{Time.frameCount}] Ejecutando hechizo {gestureType} por timeout - gesto simulado");
        }

        // Actualizar contexto con información del gesto simulado
        currentContext.commandUsed = gestureType;
        currentContext.commandDirection = direction;
        currentContext.commandIntensity = 0.7f; // Intensidad moderada porque no fue gesto real

        if (showCommandDebug)
        {
            Debug.Log($"[{Time.frameCount}] Gesto simulado: {gestureType} con intensidad: {currentContext.commandIntensity}");
        }

        // Ejecutar el hechizo
        ExecuteSpellWithContext(currentContext);
    }

    /// <summary>
    /// NUEVO: Ejecuta un hechizo direccional cuando se acaba el timeout
    /// Dispara hacia donde esté apuntando en ese momento
    /// </summary>
    private void ExecuteDirectionalTimeoutSpell()
    {
        if (showCommandDebug)
        {
            Debug.Log($"[{Time.frameCount}] Ejecutando hechizo direccional por timeout - disparando hacia la dirección actual");
        }

        // Obtener la posición objetivo actual (hacia donde apunta ahora)
        Vector3 currentTargetPosition;
        bool hasValidTarget = GetCurrentTargetPosition(out currentTargetPosition);

        // Actualizar contexto con la información actual
        if (hasValidTarget)
        {
            currentContext.targetPosition = currentTargetPosition;
            currentContext.hasValidTarget = true;
        }
        else
        {
            // Si no hay objetivo válido, usar la dirección del bastón
            Vector3 rayDirection = useUpwardRaycast ? spellSpawnPoint.up : spellSpawnPoint.forward;
            currentContext.targetPosition = spellSpawnPoint.position + rayDirection * maxRaycastDistance;
            currentContext.hasValidTarget = false;
        }

        // Configurar como comando direccional pero sin el bonus de precisión
        currentContext.commandUsed = SpellCommandType.DIRECTIONAL;
        currentContext.commandDirection = (currentContext.targetPosition - spellSpawnPoint.position).normalized;
        currentContext.commandIntensity = 0.5f; // Menor intensidad porque no fue targeting preciso

        if (showCommandDebug)
        {
            Debug.Log($"[{Time.frameCount}] Disparando hacia: {currentContext.targetPosition} | Válido: {hasValidTarget}");
        }

        // Ejecutar el hechizo
        ExecuteSpellWithContext(currentContext);
    }

    #endregion

    #region Efectos Visuales y Sonoros (Mayormente sin cambios)

    private IEnumerator FadeOutCircles()
    {
        foreach (VFXCircleEffect effect in activeEffects)
        {
            if (effect != null)
            {
                effect.Hide();
            }
        }

        yield return new WaitForSeconds(0.5f);
        ClearAllCircles();
    }

    private void PlayCastingEffects()
    {
        if (castingVFX != null && !castingVFX.isPlaying)
        {
            castingVFX.Play();
        }

        if (audioController != null)
        {
            audioController.Play(castSoundId);
        }
    }

    private void PlayChargingEffects(bool activate)
    {
        if (chargingVFX != null)
        {
            if (activate && !chargingVFX.isPlaying)
            {
                chargingVFX.Play();
                if (audioController != null)
                {
                    audioController.Play(chargingSoundId);
                }
            }
            else if (!activate && chargingVFX.isPlaying)
            {
                chargingVFX.Stop();
                if (audioController != null)
                {
                    audioController.StopSound(chargingSoundId, true);
                }
            }
        }
    }

    private void ClearAllCircles()
    {
        foreach (GameObject circle in activeCircles)
        {
            if (circle != null)
            {
                Destroy(circle);
            }
        }

        activeCircles.Clear();
        activeEffects.Clear();
    }

    private IEnumerator CreateDelayedCircle(MagicCircleConfig config)
    {
        if (!isCharging)
            yield break;

        Vector3 position = spellSpawnPoint.position +
                          spellSpawnPoint.TransformDirection(config.positionOffset);

        GameObject circle = Instantiate(config.circlePrefab,
                                       position,
                                       spellSpawnPoint.rotation,
                                       spellSpawnPoint);

        VFXCircleEffect effect = circle.GetComponent<VFXCircleEffect>();

        if (effect != null)
        {
            effect.isDecorative = false;
            effect.InitializeWithDelay(config.appearDelay, equippedSpell.MinChargeTime);
            effect.StartChargeEffect();
            activeEffects.Add(effect);
        }

        activeCircles.Add(circle);
    }

    private void PlayCancelEffects()
    {
        if (cancelVFX != null)
        {
            cancelVFX.Play();
        }

        if (audioController != null)
        {
            audioController.Play(cancelSoundId);
        }
    }

    private void PlayInsufficientManaFeedback()
    {
        Debug.Log("¡Mana insuficiente!");
    }

    private void PlayCooldownFeedback()
    {
        Debug.Log("¡Hechizo en cooldown!");
    }

    private void PlayFailedCastFeedback()
    {
        Debug.Log("¡No hay hechizo equipado!");
    }

    private void StopAllParticleEffects()
    {
        if (castingVFX != null && castingVFX.isPlaying)
            castingVFX.Stop();

        if (chargingVFX != null && chargingVFX.isPlaying)
            chargingVFX.Stop();

        if (cancelVFX != null && cancelVFX.isPlaying)
            cancelVFX.Stop();

        if (audioController != null)
        {
            audioController.StopAllSounds(false);
        }
    }

    #endregion

    #region Depuración - ACTUALIZADO

    private void OnGUI()
    {
        if (!showDebugMessages) return;

#if UNITY_EDITOR
        int y = 10;
        GUI.Label(new Rect(10, y, 500, 20), $"Estado: {currentState} | Cargando: {isCharging}");
        y += 20;

        if (currentState == SpellState.PREPARED && equippedSpell != null)
        {
            GUI.Label(new Rect(10, y, 500, 20), $"Comando requerido: {equippedSpell.CommandType}");
            y += 20;

            // Mostrar timeout para todos los tipos
            if (equippedSpell.CommandTimeout > 0)
            {
                float remainingTime = equippedSpell.CommandTimeout - (Time.time - preparationStartTime);
                GUI.Label(new Rect(10, y, 500, 20), $"Tiempo restante: {remainingTime:F1}s");
                y += 20;
            }

            // Información específica por tipo de comando
            switch (equippedSpell.CommandType)
            {
                case SpellCommandType.DIRECTIONAL:
                    GUI.Label(new Rect(10, y, 500, 20), $"Targeting: {isTargeting}");
                    y += 20;
                    if (isTargeting)
                    {
                        float progress = (Time.time - targetingStartTime) / equippedSpell.AimHoldTime;
                        GUI.Label(new Rect(10, y, 500, 20), $"Progreso targeting PRECISO: {progress:F2}");
                        y += 20;
                    }
                    else
                    {
                        GUI.Label(new Rect(10, y, 500, 20), $"Al timeout → Disparo automático hacia donde apunte");
                        y += 20;
                    }

                    // Mostrar si hay efecto de preparación activo
                    if (preparationEffect != null)
                    {
                        GUI.Label(new Rect(10, y, 500, 20), $"Proyectil flotando en: {preparationEffect.transform.position}");
                        y += 20;
                    }
                    break;

                case SpellCommandType.EMERGE:
                    GUI.Label(new Rect(10, y, 500, 20), $"Esperando gesto HACIA ARRIBA");
                    y += 20;
                    GUI.Label(new Rect(10, y, 500, 20), $"Al timeout → Emerge automáticamente");
                    y += 20;
                    break;

                case SpellCommandType.DESCEND:
                    GUI.Label(new Rect(10, y, 500, 20), $"Esperando gesto HACIA ABAJO");
                    y += 20;
                    GUI.Label(new Rect(10, y, 500, 20), $"Al timeout → Desciende automáticamente");
                    y += 20;
                    break;

                case SpellCommandType.INSTANT:
                    GUI.Label(new Rect(10, y, 500, 20), $"¡Hechizo instantáneo ejecutándose!");
                    y += 20;
                    break;
            }

            // Mostrar velocidad actual del controlador para debugging de gestos
            if (chargingController != null &&
                (equippedSpell.CommandType == SpellCommandType.EMERGE || equippedSpell.CommandType == SpellCommandType.DESCEND))
            {
                Vector3 currentVelocity = (chargingController.transform.position - lastControllerPosition) / Time.deltaTime;
                GUI.Label(new Rect(10, y, 500, 20), $"Velocidad controlador Y: {currentVelocity.y:F2} (mín: ±{gestureMinSpeed})");
                y += 20;
            }
        }
#endif
    }

    /// <summary>
    /// Visualización del raycast hacia arriba
    /// </summary>
    private void OnDrawGizmos()
    {
        if (!showTargetingDebug) return;

        // Usar el origen configurable para la visualización
        Transform actualOrigin = raycastOrigin != null ? raycastOrigin : spellSpawnPoint;
        if (actualOrigin == null) return;

        Vector3 rayOrigin = actualOrigin.position;

        Transform rotationSource = (useRaycastOriginRotation && raycastOrigin != null) ? raycastOrigin : spellSpawnPoint;
        if (rotationSource == null) return;

        Vector3 rayDirection = useUpwardRaycast ? rotationSource.up : rotationSource.forward;

        RaycastHit hitInfo;
        if (Physics.Raycast(rayOrigin, rayDirection, out hitInfo, maxRaycastDistance, raycastLayers))
        {
            // Impacto encontrado
            Gizmos.color = Color.green;
            Gizmos.DrawLine(rayOrigin, hitInfo.point);

            Gizmos.color = Color.red;
            Gizmos.DrawSphere(hitInfo.point, 0.1f);

            Gizmos.color = Color.blue;
            Gizmos.DrawLine(hitInfo.point, hitInfo.point + hitInfo.normal * 0.5f);

            // Visualizar área de tolerancia para targeting
            if (currentState == SpellState.PREPARED && equippedSpell != null &&
                equippedSpell.CommandType == SpellCommandType.DIRECTIONAL && isTargeting)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(hitInfo.point, equippedSpell.AimTolerance);
            }
        }
        else
        {
            // Sin impacto
            Gizmos.color = Color.gray;
            Gizmos.DrawLine(rayOrigin, rayOrigin + rayDirection * maxRaycastDistance);
        }

        // Mostrar el origen del raycast con un ícono especial
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(rayOrigin, 0.05f);

        // Mostrar diferencia entre origen del raycast y spawn point si son diferentes
        if (raycastOrigin != null && raycastOrigin != spellSpawnPoint)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(spellSpawnPoint.position, raycastOrigin.position);
            Gizmos.DrawWireSphere(spellSpawnPoint.position, 0.03f); // Spawn point más pequeño
        }
    }


    #endregion
}