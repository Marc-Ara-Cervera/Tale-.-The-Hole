using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using System;
using System.Collections.Generic;
using static SpellBase;
using System.Collections;

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

    // Referencias a componentes
    private XRGrabInteractable grabInteractable;

    // Lista de todos los controladores que actualmente están agarrando este bastón
    private List<XRBaseController> heldByControllers = new List<XRBaseController>();

    // Referencia a las estadísticas del jugador
    private PlayerStatsManager playerStats;

    // Estado de carga
    private bool isCharging = false;
    private XRBaseController chargingController = null;
    private float chargeStartTime; // Tiempo en que comenzó la carga
    private bool chargeComplete; // Indica si la carga ha completado el 100%
    private float lastProgress = -1f; // Para optimización

    // Para depuración
    [SerializeField] private bool showDebugMessages = true;

    // Lista para seguir los círculos activos
    private List<GameObject> activeCircles = new List<GameObject>();
    private List<VFXCircleEffect> activeEffects = new List<VFXCircleEffect>();

    // Eventos
    public event Action<SpellBase> OnSpellCast;
    public event Action<bool> OnSpellChargeStateChanged;

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
        isCharging = false;
        chargingController = null;
        heldByControllers.Clear();
        StopAllParticleEffects();
        ClearAllCircles();
    }

    #endregion

    #region Gestión de Interacción VR

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

        // Si este controlador ya estaba en la lista, evitar duplicados
        if (heldByControllers.Contains(newController))
            return;

        // Añadir este controlador a la lista de controladores que sostienen el bastón
        heldByControllers.Add(newController);

        // Verificar si es un controlador dominante para asignar playerStats
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

        // Eliminar este controlador de la lista
        heldByControllers.Remove(releasedController);

        // Si estábamos cargando con este controlador, cancelar la carga
        if (isCharging && releasedController == chargingController)
        {
            if (showDebugMessages)
            {
                Debug.Log($"[{Time.frameCount}] Cancelando carga porque el controlador que estaba cargando soltó el bastón");
            }
            CancelCharging(releasedController);
        }

        // Si ya no queda ningún controlador sosteniendo el bastón, reiniciar todo
        if (heldByControllers.Count == 0)
        {
            playerStats = null;
            if (showDebugMessages)
            {
                Debug.Log($"[{Time.frameCount}] Ningún controlador sostiene el bastón, reiniciando estado");
            }
        }
        else
        {
            // Comprobar si queda algún controlador dominante sosteniendo el bastón
            foreach (XRBaseController controller in heldByControllers)
            {
                SpellCastController spellController = controller.GetComponent<SpellCastController>();
                if (spellController != null && spellController.IsDominantHand)
                {
                    playerStats = controller.transform.root.GetComponent<PlayerStatsManager>();

                    if (showDebugMessages)
                    {
                        Debug.Log($"[{Time.frameCount}] Todavía hay un controlador dominante sosteniendo el bastón: {controller.name}");
                    }
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

    #region Sistema de Carga y Lanzamiento de Hechizos

    /// <summary>
    /// Comienza la carga de un hechizo
    /// </summary>
    public void StartCharging(XRBaseController requestingController)
    {
        // Verificación rigurosa: solo el controlador dominante que está sosteniendo el bastón puede cargar
        if (!IsHeldByDominantHand(requestingController))
        {
            if (showDebugMessages)
            {
                Debug.Log($"[{Time.frameCount}] Ignorando StartCharging de {requestingController.name} - No es dominante o no sostiene el bastón");
            }
            return;
        }

        // Verificaciones adicionales
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

        // Si ya estamos cargando con otro controlador, cancelar esa carga primero
        if (isCharging && chargingController != null && chargingController != requestingController)
        {
            CancelCharging(chargingController);
        }

        // Limpiar círculos anteriores por si acaso
        ClearAllCircles();

        // Activar estado de carga
        isCharging = true;
        chargingController = requestingController;
        chargeStartTime = Time.time; // Registrar tiempo de inicio
        chargeComplete = false; // Reiniciar estado de carga completa
        lastProgress = -1f; // Reiniciar progreso anterior

        if (showDebugMessages)
        {
            Debug.Log($"[{Time.frameCount}] Iniciando carga de hechizo con controlador: {requestingController.name}");
        }

        // Verificar si el hechizo tiene configuraciones de círculos
        if (equippedSpell != null && equippedSpell.HasMagicCircles())
        {
            MagicCircleConfig[] circleConfigs = equippedSpell.GetMagicCircles();

            // Crear cada círculo configurado
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
    /// Finaliza la carga y lanza el hechizo si se cumple el tiempo mínimo
    /// </summary>
    public void FinishCharging(XRBaseController requestingController, float chargeTime)
    {
        // Verificación rigurosa: solo el controlador que inició la carga puede finalizarla
        if (chargingController != requestingController)
        {
            if (showDebugMessages)
            {
                Debug.Log($"[{Time.frameCount}] Ignorando FinishCharging de {requestingController.name} - No es el controlador que inició la carga");
            }
            return;
        }

        // Verificación adicional: asegurarse de que seguimos en estado de carga
        if (!isCharging)
        {
            if (showDebugMessages)
            {
                Debug.Log($"[{Time.frameCount}] Ignorando FinishCharging porque no estamos en estado de carga");
            }
            return;
        }

        // Desactivar estado de carga
        isCharging = false;

        // Detener efectos visuales de carga
        PlayChargingEffects(false);

        // Notificar cambio de estado
        OnSpellChargeStateChanged?.Invoke(false);

        if (showDebugMessages)
        {
            Debug.Log($"[{Time.frameCount}] Finalizando carga de hechizo con tiempo: {chargeTime}s");
        }

        // Verificar si hay un hechizo equipado
        if (equippedSpell == null)
        {
            PlayFailedCastFeedback();
            ClearAllCircles();
            return;
        }

        // Verificar tiempo mínimo de carga del hechizo equipado
        if (chargeTime < equippedSpell.MinChargeTime)
        {
            if (showDebugMessages)
            {
                Debug.Log($"[{Time.frameCount}] Tiempo de carga insuficiente. Requerido: {equippedSpell.MinChargeTime}s, Actual: {chargeTime}s");
            }
            PlayCancelEffects();
            ClearAllCircles();
            return;
        }

        // Verificar si tenemos suficiente mana
        if (playerStats == null || !playerStats.Mana.CanCastSpell(equippedSpell.ManaCost))
        {
            PlayInsufficientManaFeedback();
            ClearAllCircles();
            return;
        }

        // Verificar si el hechizo está listo (cooldown)
        if (!equippedSpell.IsReady())
        {
            if (showDebugMessages)
            {
                Debug.Log($"[{Time.frameCount}] Hechizo en cooldown");
            }
            PlayCooldownFeedback();
            ClearAllCircles();
            return;
        }

        // Iniciar animación de desaparición de círculos antes de lanzar
        StartCoroutine(FadeOutCircles());

        // Lanzar el hechizo
        if (showDebugMessages)
        {
            Debug.Log($"[{Time.frameCount}] ¡Lanzando hechizo!");
        }
        equippedSpell.Cast(spellSpawnPoint, playerStats);

        // Consumir mana
        playerStats.Mana.CastSpell(equippedSpell.ManaCost);

        // Efectos visuales/sonoros de lanzamiento
        PlayCastingEffects();

        // Reiniciar estado
        chargingController = null;
        chargeComplete = false;

        // Notificar a los suscriptores
        OnSpellCast?.Invoke(equippedSpell);
    }

    /// <summary>
    /// Cancela la carga actual
    /// </summary>
    public void CancelCharging(XRBaseController requestingController)
    {
        // Solo permitir cancelar al controlador que inició la carga o a un controlador dominante
        if (chargingController != requestingController && !IsHeldByDominantHand(requestingController))
        {
            if (showDebugMessages)
            {
                Debug.Log($"[{Time.frameCount}] Ignorando CancelCharging de {requestingController.name} - No es el controlador adecuado");
            }
            return;
        }

        // Solo procesar si estábamos cargando
        if (!isCharging)
        {
            if (showDebugMessages)
            {
                Debug.Log($"[{Time.frameCount}] Ignorando CancelCharging porque no estamos en estado de carga");
            }
            return;
        }

        if (showDebugMessages)
        {
            Debug.Log($"[{Time.frameCount}] Cancelando carga de hechizo");
        }

        // Desactivar estado de carga
        isCharging = false;

        // Detener efectos visuales de carga y sonido
        PlayChargingEffects(false);

        // Mostrar efectos de cancelación
        PlayCancelEffects();

        // Iniciar desaparición gradual de círculos
        StartCoroutine(FadeOutCircles());

        // Reiniciar estado
        chargingController = null;
        chargeComplete = false;

        // Notificar cambio de estado
        OnSpellChargeStateChanged?.Invoke(false);
    }

    private void Update()
    {
        // Solo procesar si estamos en estado de carga
        if (!isCharging || equippedSpell == null) return;

        // Calcular progreso de carga
        float chargeTime = Time.time - chargeStartTime;
        float progress = Mathf.Clamp01(chargeTime / equippedSpell.MinChargeTime);

        // Solo actualizar si hay un cambio significativo (optimización)
        if (Mathf.Abs(lastProgress - progress) > 0.01f)
        {
            lastProgress = progress;

            // Actualizar círculos mágicos
            foreach (VFXCircleEffect effect in activeEffects)
            {
                if (effect == null) continue;
                effect.UpdateProgress(progress);
            }

            // Completar la carga cuando llegue al 100%
            if (progress >= 1.0f && !chargeComplete)
            {
                chargeComplete = true;

                // Opcional: Feedback de vibración
                // (código para vibración si existe)
            }
        }
    }

    #endregion

    #region Efectos Visuales y Sonoros

    private IEnumerator FadeOutCircles()
    {
        // Iniciar animación de desaparición en cada círculo
        foreach (VFXCircleEffect effect in activeEffects)
        {
            if (effect != null)
            {
                effect.Hide();
            }
        }

        // Esperar un tiempo para la animación
        yield return new WaitForSeconds(0.5f);

        // Limpiar
        ClearAllCircles();
    }

    /// <summary>
    /// Activa efectos visuales y sonoros al lanzar un hechizo
    /// </summary>
    private void PlayCastingEffects()
    {
        if (castingVFX != null && !castingVFX.isPlaying)
        {
            castingVFX.Play();
        }

        // Usar el nuevo sistema de audio
        if (audioController != null)
        {
            audioController.Play(castSoundId);
        }
    }

    /// <summary>
    /// Activa/desactiva efectos visuales durante la carga
    /// </summary>
    private void PlayChargingEffects(bool activate)
    {
        if (chargingVFX != null)
        {
            if (activate && !chargingVFX.isPlaying)
            {
                chargingVFX.Play();

                // Utiliza la configuración de fade específica del sonido
                if (audioController != null)
                {
                    // El método Play ahora usará automáticamente el fade configurado
                    audioController.Play(chargingSoundId);
                }
            }
            else if (!activate && chargingVFX.isPlaying)
            {
                chargingVFX.Stop();

                // Al detener, también usará la configuración de fade específica
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
        // Si ya no estamos cargando, salir
        if (!isCharging)
            yield break;

        // Calcular posición
        Vector3 position = spellSpawnPoint.position +
                          spellSpawnPoint.TransformDirection(config.positionOffset);

        // Instanciar círculo
        GameObject circle = Instantiate(config.circlePrefab,
                                       position,
                                       spellSpawnPoint.rotation,
                                       spellSpawnPoint);

        // Obtener componente
        VFXCircleEffect effect = circle.GetComponent<VFXCircleEffect>();

        // Si tiene el componente, activar efecto
        if (effect != null)
        {
            effect.isDecorative = false;

            // IMPORTANTE: Configurar tiempo total de carga y delay específico
            effect.InitializeWithDelay(config.appearDelay, equippedSpell.MinChargeTime);

            // Iniciar efecto
            effect.StartChargeEffect();

            // Registrar para actualizaciones
            activeEffects.Add(effect);
        }

        // Registrar para limpieza
        activeCircles.Add(circle);
    }

    /// <summary>
    /// Activa efectos visuales de cancelación
    /// </summary>
    private void PlayCancelEffects()
    {
        if (cancelVFX != null)
        {
            cancelVFX.Play();
        }

        // Usar el nuevo sistema de audio
        if (audioController != null)
        {
            audioController.Play(cancelSoundId);
        }
    }

    /// <summary>
    /// Feedback cuando no hay suficiente mana
    /// </summary>
    private void PlayInsufficientManaFeedback()
    {
        Debug.Log("¡Mana insuficiente!");
    }

    /// <summary>
    /// Feedback cuando el hechizo está en cooldown
    /// </summary>
    private void PlayCooldownFeedback()
    {
        Debug.Log("¡Hechizo en cooldown!");
    }

    /// <summary>
    /// Feedback cuando falla el lanzamiento por otras razones
    /// </summary>
    private void PlayFailedCastFeedback()
    {
        Debug.Log("¡No hay hechizo equipado!");
    }

    /// <summary>
    /// Detiene todos los efectos de partículas
    /// </summary>
    private void StopAllParticleEffects()
    {
        if (castingVFX != null && castingVFX.isPlaying)
            castingVFX.Stop();

        if (chargingVFX != null && chargingVFX.isPlaying)
            chargingVFX.Stop();

        if (cancelVFX != null && cancelVFX.isPlaying)
            cancelVFX.Stop();

        // Detener todos los sonidos
        if (audioController != null)
        {
            audioController.StopAllSounds(false);
        }
    }

    #endregion

    #region Depuración

    // Método para ayudar a depurar el estado actual
    private void OnGUI()
    {
        if (!showDebugMessages) return;

        // Solo para depuración en editor
#if UNITY_EDITOR
        int y = 10;
        GUI.Label(new Rect(10, y, 500, 20), $"MagicStaff Status: isCharging={isCharging}");
        y += 20;

        if (chargingController != null)
        {
            GUI.Label(new Rect(10, y, 500, 20), $"Charging Controller: {chargingController.name}");
            y += 20;
        }

        GUI.Label(new Rect(10, y, 500, 20), $"Controllers holding staff: {heldByControllers.Count}");
        y += 20;

        foreach (XRBaseController controller in heldByControllers)
        {
            SpellCastController spellController = controller.GetComponent<SpellCastController>();
            bool isDominant = spellController != null && spellController.IsDominantHand;
            GUI.Label(new Rect(10, y, 500, 20), $"- {controller.name} (Dominant: {isDominant})");
            y += 20;
        }
#endif
    }

    #endregion
}