using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using System;

/// <summary>
/// Script principal para el bastón mágico en VR.
/// Gestiona la interacción con los controladores VR y el lanzamiento del hechizo equipado.
/// </summary>
public class MagicStaff : MonoBehaviour
{
    #region Variables y Referencias

    [Header("Configuración del Bastón")]
    [Tooltip("Punto desde donde se generará el hechizo")]
    [SerializeField] private Transform spellSpawnPoint;

    [Tooltip("Efecto visual al lanzar un hechizo")]
    [SerializeField] private ParticleSystem castingVFX;

    [Tooltip("Efecto de sonido al lanzar un hechizo")]
    [SerializeField] private AudioSource castingAudioSource;

    [Header("Hechizo")]
    [Tooltip("Hechizo equipado en el bastón")]
    [SerializeField] private SpellBase equippedSpell;

    [Header("Sistema de Carga")]
    [Tooltip("Efecto visual durante la carga del hechizo")]
    [SerializeField] private ParticleSystem chargingVFX;

    [Tooltip("Efecto visual cuando se cancela un hechizo")]
    [SerializeField] private ParticleSystem cancelVFX;

    // Referencias a componentes
    private XRGrabInteractable grabInteractable;
    private XRBaseController currentController;

    // Referencia a las estadísticas del jugador
    private PlayerStatsManager playerStats;

    // Estado de carga
    private bool isCharging = false;

    // Referencias a efectos activos
    private GameObject activeSpellChargingEffect = null;

    // Eventos
    public event Action<SpellBase> OnSpellCast;
    public event Action<bool> OnSpellChargeStateChanged; // true = inicio, false = fin

    #endregion

    #region Inicialización y Configuración

    private void Awake()
    {
        // Obtener referencias a componentes
        grabInteractable = GetComponent<XRGrabInteractable>();

        if (spellSpawnPoint == null)
        {
            // Si no se asignó un punto de generación, usar la posición del bastón
            spellSpawnPoint = transform;
            Debug.LogWarning("No se asignó un punto de generación de hechizos. Usando la posición del bastón.");
        }

        // Verificar que tengamos los componentes necesarios
        if (grabInteractable == null)
        {
            Debug.LogError("Missing XRGrabInteractable component on magic staff!");
            return;
        }
    }

    private void OnEnable()
    {
        // Suscribir a eventos de interacción
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.AddListener(OnGrabbed);
            grabInteractable.selectExited.AddListener(OnReleased);
        }
    }

    private void OnDisable()
    {
        // Cancelar suscripciones a eventos
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnGrabbed);
            grabInteractable.selectExited.RemoveListener(OnReleased);
        }

        // Asegurarse de detener cualquier efecto si se desactiva el objeto
        StopAllParticleEffects();

        // Limpiar referencias a efectos
        activeSpellChargingEffect = null;

        // Asegurarse de detener cualquier efecto si se desactiva el objeto
        StopAllParticleEffects();
    }

    #endregion

    #region Gestión de Interacción VR

    /// <summary>
    /// Se llama cuando el bastón es agarrado por un controlador VR
    /// </summary>
    public void OnGrabbed(SelectEnterEventArgs args)
    {
        // Verificar si el interactor es un controlador XR
        XRBaseController controller = args.interactorObject.transform.GetComponent<XRBaseController>();
        if (controller != null)
        {
            // Registrar qué controlador tiene el bastón
            currentController = controller;

            // Obtener las estadísticas del jugador (necesario para verificar mana)
            playerStats = currentController.transform.root.GetComponent<PlayerStatsManager>();
        }
    }

    /// <summary>
    /// Se llama cuando el bastón es soltado
    /// </summary>
    public void OnReleased(SelectExitEventArgs args)
    {
        // Si estábamos cargando, cancelar la carga
        if (isCharging)
        {
            CancelCharging(currentController);
        }

        // Eliminar la referencia al controlador
        currentController = null;
    }

    /// <summary>
    /// Verifica si este bastón está siendo sostenido por un controlador específico
    /// </summary>
    public bool IsHeldBy(XRBaseController controller)
    {
        return currentController == controller;
    }

    #endregion

    #region Sistema de Carga y Lanzamiento de Hechizos

    /// <summary>
    /// Comienza la carga de un hechizo
    /// </summary>
    public void StartCharging(XRBaseController requestingController)
    {
        // Verificar si este bastón está siendo sostenido por el controlador que solicita cargar
        if (currentController != requestingController)
        {
            return;
        }

        // Verificar si hay un hechizo equipado
        if (equippedSpell == null)
        {
            PlayFailedCastFeedback();
            return;
        }

        // Verificar si tenemos suficiente mana
        if (playerStats == null || !playerStats.Mana.CanCastSpell(equippedSpell.ManaCost))
        {
            PlayInsufficientManaFeedback();
            return;
        }

        // Activar estado de carga
        isCharging = true;

        // Efectos visuales de carga (del bastón)
        PlayChargingEffects(true);

        // Activar efectos específicos del hechizo si existen
        if (equippedSpell != null && equippedSpell.HasCustomChargingEffect)
        {
            activeSpellChargingEffect = equippedSpell.CreateChargingEffect(spellSpawnPoint);
        }

        // Notificar cambio de estado
        OnSpellChargeStateChanged?.Invoke(true);
    }

    /// <summary>
    /// Finaliza la carga y lanza el hechizo si se cumple el tiempo mínimo
    /// </summary>
    public void FinishCharging(XRBaseController requestingController, float chargeTime)
    {
        // Verificar si este bastón está siendo sostenido por el controlador que solicita lanzar
        if (currentController != requestingController)
        {
            return;
        }

        // Desactivar estado de carga
        isCharging = false;

        // Detener efectos visuales de carga (del bastón)
        PlayChargingEffects(false);

        // Detener efectos específicos del hechizo
        if (equippedSpell != null)
        {
            equippedSpell.StopChargingEffect();
            activeSpellChargingEffect = null;
        }

        // Notificar cambio de estado
        OnSpellChargeStateChanged?.Invoke(false);

        // Verificar si hay un hechizo equipado
        if (equippedSpell == null)
        {
            PlayFailedCastFeedback();
            return;
        }

        // Verificar tiempo mínimo de carga del hechizo
        if (chargeTime < equippedSpell.MinChargeTime)
        {
            PlayCancelEffects();

            // Mostrar efecto de cancelación específico del hechizo
            if (equippedSpell.HasCustomCancelEffect)
            {
                equippedSpell.CreateCancelEffect(spellSpawnPoint.position, spellSpawnPoint.rotation);
            }

            Debug.Log($"Tiempo de carga insuficiente. Requerido: {equippedSpell.MinChargeTime}s, Actual: {chargeTime}s");
            return;
        }

        // Verificar si tenemos suficiente mana
        if (playerStats == null || !playerStats.Mana.CanCastSpell(equippedSpell.ManaCost))
        {
            PlayInsufficientManaFeedback();
            return;
        }

        // Verificar si el hechizo está listo (cooldown)
        Debug.Log($"Checking if spell is ready. Current spell: {equippedSpell.SpellName}");

        if (!equippedSpell.IsReady())
        {
            Debug.Log($"Spell {equippedSpell.SpellName} is not ready! Cooldown remaining: {equippedSpell.RemainingCooldown}");
            PlayCooldownFeedback();
            return;
        }

        Debug.Log($"Spell {equippedSpell.SpellName} is ready to cast!");

        // Lanzar el hechizo
        equippedSpell.Cast(spellSpawnPoint, playerStats);

        // Consumir mana
        playerStats.Mana.CastSpell(equippedSpell.ManaCost);

        // Efectos visuales/sonoros de lanzamiento
        PlayCastingEffects();

        // Notificar a los suscriptores
        OnSpellCast?.Invoke(equippedSpell);
    }

    /// <summary>
    /// Cancela la carga actual (cuando el jugador suelta el bastón durante la carga)
    /// </summary>
    public void CancelCharging(XRBaseController requestingController)
    {
        // Verificar si este bastón está siendo sostenido por el controlador que solicita cancelar
        if (currentController != requestingController)
        {
            return;
        }

        // Solo procesar si estábamos cargando
        if (!isCharging)
        {
            return;
        }

        // Desactivar estado de carga
        isCharging = false;

        // Detener efectos visuales de carga (del bastón)
        PlayChargingEffects(false);

        // Detener efectos específicos del hechizo
        if (equippedSpell != null)
        {
            equippedSpell.StopChargingEffect();
            activeSpellChargingEffect = null;

            // Mostrar efecto de cancelación específico del hechizo
            if (equippedSpell.HasCustomCancelEffect)
            {
                equippedSpell.CreateCancelEffect(spellSpawnPoint.position, spellSpawnPoint.rotation);
            }
        }

        // Mostrar efectos de cancelación (del bastón)
        PlayCancelEffects();

        // Notificar cambio de estado
        OnSpellChargeStateChanged?.Invoke(false);
    }

    #endregion

    #region Efectos Visuales y Sonoros

    /// <summary>
    /// Activa efectos visuales y sonoros al lanzar un hechizo
    /// </summary>
    private void PlayCastingEffects()
    {
        // Activar efecto de partículas
        if (castingVFX != null && !castingVFX.isPlaying)
        {
            castingVFX.Play();
        }

        // Activar efecto de sonido
        if (castingAudioSource != null)
        {
            castingAudioSource.Play();
        }
    }

    /// <summary>
    /// Activa/desactiva efectos visuales durante la carga
    /// </summary>
    private void PlayChargingEffects(bool activate)
    {
        // Efectos de partículas
        if (chargingVFX != null)
        {
            if (activate && !chargingVFX.isPlaying)
            {
                chargingVFX.Play();
            }
            else if (!activate && chargingVFX.isPlaying)
            {
                chargingVFX.Stop();
            }
        }
    }

    /// <summary>
    /// Activa efectos visuales de cancelación
    /// </summary>
    private void PlayCancelEffects()
    {
        // Efectos de partículas
        if (cancelVFX != null)
        {
            cancelVFX.Play();
        }
    }

    /// <summary>
    /// Feedback cuando no hay suficiente mana
    /// </summary>
    private void PlayInsufficientManaFeedback()
    {
        // Efecto visual/auditivo de mana insuficiente
        Debug.Log("¡Mana insuficiente!");
        // Aquí podrías añadir un efecto de partículas específico para mana insuficiente
    }

    /// <summary>
    /// Feedback cuando el hechizo está en cooldown
    /// </summary>
    private void PlayCooldownFeedback()
    {
        // Efecto visual/auditivo de hechizo en cooldown
        Debug.Log("¡Hechizo en cooldown!");
        // Aquí podrías añadir un efecto de partículas específico para cooldown
    }

    /// <summary>
    /// Feedback cuando falla el lanzamiento por otras razones
    /// </summary>
    private void PlayFailedCastFeedback()
    {
        // Efecto visual/auditivo de lanzamiento fallido
        Debug.Log("¡No hay hechizo equipado!");
        // Aquí podrías añadir un efecto de partículas específico para lanzamiento fallido
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

        // Detener efectos del hechizo activo
        if (equippedSpell != null)
        {
            equippedSpell.StopChargingEffect();
            activeSpellChargingEffect = null;
        }
    }

    #endregion

    #region Preparación para Multiplayer (Futuro)

    // Estos métodos se implementarán cuando se añada Photon PUN 2

    // private void TransferOwnership()
    // {
    //     // Cuando se implemente Photon, aquí transferiremos la propiedad al jugador que agarre el bastón
    //     // photonView.TransferOwnership(PhotonNetwork.LocalPlayer);
    // }

    // private bool IsOwner()
    // {
    //     // Verificar si el jugador local es el dueño de este bastón
    //     // return photonView.IsMine;
    //     return true; // Por ahora, siempre es true en singleplayer
    // }

    #endregion
}