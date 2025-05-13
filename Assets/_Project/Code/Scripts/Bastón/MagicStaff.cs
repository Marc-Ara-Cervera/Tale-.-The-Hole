using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using System;

/// <summary>
/// Script principal para el bast�n m�gico en VR.
/// Gestiona la interacci�n con los controladores VR y el lanzamiento del hechizo equipado.
/// </summary>
public class MagicStaff : MonoBehaviour
{
    #region Variables y Referencias

    [Header("Configuraci�n del Bast�n")]
    [Tooltip("Punto desde donde se generar� el hechizo")]
    [SerializeField] private Transform spellSpawnPoint;

    [Tooltip("Efecto visual al lanzar un hechizo")]
    [SerializeField] private ParticleSystem castingVFX;

    [Tooltip("Efecto de sonido al lanzar un hechizo")]
    [SerializeField] private AudioSource castingAudioSource;

    [Header("Hechizo")]
    [Tooltip("Hechizo equipado en el bast�n")]
    [SerializeField] private SpellBase equippedSpell;

    [Header("Sistema de Carga")]
    [Tooltip("Efecto visual durante la carga del hechizo")]
    [SerializeField] private ParticleSystem chargingVFX;

    [Tooltip("Efecto visual cuando se cancela un hechizo")]
    [SerializeField] private ParticleSystem cancelVFX;

    // Referencias a componentes
    private XRGrabInteractable grabInteractable;
    private XRBaseController currentController;

    // Referencia a las estad�sticas del jugador
    private PlayerStatsManager playerStats;

    // Estado de carga
    private bool isCharging = false;

    // Referencias a efectos activos
    private GameObject activeSpellChargingEffect = null;

    // Eventos
    public event Action<SpellBase> OnSpellCast;
    public event Action<bool> OnSpellChargeStateChanged; // true = inicio, false = fin

    #endregion

    #region Inicializaci�n y Configuraci�n

    private void Awake()
    {
        // Obtener referencias a componentes
        grabInteractable = GetComponent<XRGrabInteractable>();

        if (spellSpawnPoint == null)
        {
            // Si no se asign� un punto de generaci�n, usar la posici�n del bast�n
            spellSpawnPoint = transform;
            Debug.LogWarning("No se asign� un punto de generaci�n de hechizos. Usando la posici�n del bast�n.");
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
        // Suscribir a eventos de interacci�n
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

    #region Gesti�n de Interacci�n VR

    /// <summary>
    /// Se llama cuando el bast�n es agarrado por un controlador VR
    /// </summary>
    public void OnGrabbed(SelectEnterEventArgs args)
    {
        // Verificar si el interactor es un controlador XR
        XRBaseController controller = args.interactorObject.transform.GetComponent<XRBaseController>();
        if (controller != null)
        {
            // Registrar qu� controlador tiene el bast�n
            currentController = controller;

            // Obtener las estad�sticas del jugador (necesario para verificar mana)
            playerStats = currentController.transform.root.GetComponent<PlayerStatsManager>();
        }
    }

    /// <summary>
    /// Se llama cuando el bast�n es soltado
    /// </summary>
    public void OnReleased(SelectExitEventArgs args)
    {
        // Si est�bamos cargando, cancelar la carga
        if (isCharging)
        {
            CancelCharging(currentController);
        }

        // Eliminar la referencia al controlador
        currentController = null;
    }

    /// <summary>
    /// Verifica si este bast�n est� siendo sostenido por un controlador espec�fico
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
        // Verificar si este bast�n est� siendo sostenido por el controlador que solicita cargar
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

        // Efectos visuales de carga (del bast�n)
        PlayChargingEffects(true);

        // Activar efectos espec�ficos del hechizo si existen
        if (equippedSpell != null && equippedSpell.HasCustomChargingEffect)
        {
            activeSpellChargingEffect = equippedSpell.CreateChargingEffect(spellSpawnPoint);
        }

        // Notificar cambio de estado
        OnSpellChargeStateChanged?.Invoke(true);
    }

    /// <summary>
    /// Finaliza la carga y lanza el hechizo si se cumple el tiempo m�nimo
    /// </summary>
    public void FinishCharging(XRBaseController requestingController, float chargeTime)
    {
        // Verificar si este bast�n est� siendo sostenido por el controlador que solicita lanzar
        if (currentController != requestingController)
        {
            return;
        }

        // Desactivar estado de carga
        isCharging = false;

        // Detener efectos visuales de carga (del bast�n)
        PlayChargingEffects(false);

        // Detener efectos espec�ficos del hechizo
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

        // Verificar tiempo m�nimo de carga del hechizo
        if (chargeTime < equippedSpell.MinChargeTime)
        {
            PlayCancelEffects();

            // Mostrar efecto de cancelaci�n espec�fico del hechizo
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

        // Verificar si el hechizo est� listo (cooldown)
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
    /// Cancela la carga actual (cuando el jugador suelta el bast�n durante la carga)
    /// </summary>
    public void CancelCharging(XRBaseController requestingController)
    {
        // Verificar si este bast�n est� siendo sostenido por el controlador que solicita cancelar
        if (currentController != requestingController)
        {
            return;
        }

        // Solo procesar si est�bamos cargando
        if (!isCharging)
        {
            return;
        }

        // Desactivar estado de carga
        isCharging = false;

        // Detener efectos visuales de carga (del bast�n)
        PlayChargingEffects(false);

        // Detener efectos espec�ficos del hechizo
        if (equippedSpell != null)
        {
            equippedSpell.StopChargingEffect();
            activeSpellChargingEffect = null;

            // Mostrar efecto de cancelaci�n espec�fico del hechizo
            if (equippedSpell.HasCustomCancelEffect)
            {
                equippedSpell.CreateCancelEffect(spellSpawnPoint.position, spellSpawnPoint.rotation);
            }
        }

        // Mostrar efectos de cancelaci�n (del bast�n)
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
        // Activar efecto de part�culas
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
        // Efectos de part�culas
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
    /// Activa efectos visuales de cancelaci�n
    /// </summary>
    private void PlayCancelEffects()
    {
        // Efectos de part�culas
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
        Debug.Log("�Mana insuficiente!");
        // Aqu� podr�as a�adir un efecto de part�culas espec�fico para mana insuficiente
    }

    /// <summary>
    /// Feedback cuando el hechizo est� en cooldown
    /// </summary>
    private void PlayCooldownFeedback()
    {
        // Efecto visual/auditivo de hechizo en cooldown
        Debug.Log("�Hechizo en cooldown!");
        // Aqu� podr�as a�adir un efecto de part�culas espec�fico para cooldown
    }

    /// <summary>
    /// Feedback cuando falla el lanzamiento por otras razones
    /// </summary>
    private void PlayFailedCastFeedback()
    {
        // Efecto visual/auditivo de lanzamiento fallido
        Debug.Log("�No hay hechizo equipado!");
        // Aqu� podr�as a�adir un efecto de part�culas espec�fico para lanzamiento fallido
    }

    /// <summary>
    /// Detiene todos los efectos de part�culas
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

    #region Preparaci�n para Multiplayer (Futuro)

    // Estos m�todos se implementar�n cuando se a�ada Photon PUN 2

    // private void TransferOwnership()
    // {
    //     // Cuando se implemente Photon, aqu� transferiremos la propiedad al jugador que agarre el bast�n
    //     // photonView.TransferOwnership(PhotonNetwork.LocalPlayer);
    // }

    // private bool IsOwner()
    // {
    //     // Verificar si el jugador local es el due�o de este bast�n
    //     // return photonView.IsMine;
    //     return true; // Por ahora, siempre es true en singleplayer
    // }

    #endregion
}