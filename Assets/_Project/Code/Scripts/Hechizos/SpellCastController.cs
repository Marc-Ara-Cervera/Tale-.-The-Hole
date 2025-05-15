using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.InputSystem;

public class SpellCastController : MonoBehaviour
{
    [Header("Configuración de Mano")]
    [Tooltip("¿Es esta la mano dominante para lanzar hechizos?")]
    [SerializeField] private bool isDominantHand = false;

    [Header("Referencias")]
    [SerializeField] private XRBaseController controller;

    [Header("Configuración de Input")]
    [SerializeField] private InputActionReference castSpellAction;

    [Header("Debug")]
    [SerializeField] private bool showDebugMessages = true;

    // Cache de bastones
    private MagicStaff[] cachedStaffs;
    private float cacheUpdateTime = 0f;
    private float cacheLifetime = 1f;

    // Estado de carga
    private bool isCharging = false;
    private float chargeStartTime = 0f;

    // Propiedad pública para acceder desde otros scripts
    public bool IsDominantHand => isDominantHand;
    public XRBaseController Controller => controller;

    private void Awake()
    {
        if (controller == null)
        {
            controller = GetComponent<XRBaseController>();
            if (controller == null)
            {
                Debug.LogError("No se encontró un XRBaseController en el GameObject.");
            }
        }

        if (showDebugMessages)
        {
            Debug.Log($"Controlador {controller.name} inicializado. ¿Es dominante? {isDominantHand}");
        }
    }

    private void OnEnable()
    {
        if (castSpellAction != null)
        {
            castSpellAction.action.Enable();

            if (isDominantHand)
            {
                // Solo registramos los eventos de input en la mano dominante
                castSpellAction.action.started += OnSpellChargeStarted;
                castSpellAction.action.canceled += OnSpellChargeEnded;

                if (showDebugMessages)
                {
                    Debug.Log($"Controlador dominante {controller.name} suscrito a eventos de hechizos");
                }
            }
        }
    }

    private void OnDisable()
    {
        if (castSpellAction != null)
        {
            castSpellAction.action.Disable();

            if (isDominantHand)
            {
                castSpellAction.action.started -= OnSpellChargeStarted;
                castSpellAction.action.canceled -= OnSpellChargeEnded;
            }
        }

        // Asegurarse de limpiar el estado al desactivar
        ResetChargeState();
    }

    /// <summary>
    /// Llamado cuando el botón de hechizo comienza a ser presionado
    /// </summary>
    private void OnSpellChargeStarted(InputAction.CallbackContext context)
    {
        if (!isDominantHand)
            return;

        if (showDebugMessages)
        {
            Debug.Log($"[{Time.frameCount}] Comenzando carga de hechizo con controlador dominante: {controller.name}");
        }

        // Verificar si estamos sosteniendo algún bastón antes de intentar cargar
        UpdateStaffsCache();
        bool holdingAnyStaff = false;

        foreach (MagicStaff staff in cachedStaffs)
        {
            if (staff != null && staff.IsHeldByDominantHand(controller))
            {
                holdingAnyStaff = true;
                break;
            }
        }

        if (!holdingAnyStaff)
        {
            if (showDebugMessages)
            {
                Debug.Log($"[{Time.frameCount}] No se encontró ningún bastón sostenido por la mano dominante, ignorando input");
            }
            return;
        }

        // Iniciar carga
        isCharging = true;
        chargeStartTime = Time.time;

        // Notificar a todos los bastones - solo responderá el que esté sostenido por esta mano
        foreach (MagicStaff staff in cachedStaffs)
        {
            if (staff != null)
            {
                staff.StartCharging(controller);
            }
        }
    }

    /// <summary>
    /// Llamado cuando el botón de hechizo es liberado
    /// </summary>
    private void OnSpellChargeEnded(InputAction.CallbackContext context)
    {
        if (!isDominantHand || !isCharging)
            return;

        float chargeTime = Time.time - chargeStartTime;

        if (showDebugMessages)
        {
            Debug.Log($"[{Time.frameCount}] Finalizando carga de hechizo con controlador dominante: {controller.name}. Tiempo: {chargeTime}s");
        }

        // Resetear estado interno
        isCharging = false;

        // Notificar a todos los bastones
        UpdateStaffsCache();
        foreach (MagicStaff staff in cachedStaffs)
        {
            if (staff != null)
            {
                staff.FinishCharging(controller, chargeTime);
            }
        }
    }

    /// <summary>
    /// Cancela la carga actual
    /// </summary>
    public void CancelSpellCharge()
    {
        if (!isDominantHand || !isCharging)
            return;

        if (showDebugMessages)
        {
            Debug.Log($"[{Time.frameCount}] Carga cancelada con controlador dominante: {controller.name}");
        }

        // Resetear estado interno
        ResetChargeState();

        // Notificar a todos los bastones
        UpdateStaffsCache();
        foreach (MagicStaff staff in cachedStaffs)
        {
            if (staff != null)
            {
                staff.CancelCharging(controller);
            }
        }
    }

    /// <summary>
    /// Resetea el estado de carga interno
    /// </summary>
    public void ResetChargeState()
    {
        isCharging = false;
        chargeStartTime = 0f;
    }

    /// <summary>
    /// Actualiza la caché de bastones
    /// </summary>
    private void UpdateStaffsCache()
    {
        if (Time.time - cacheUpdateTime > cacheLifetime || cachedStaffs == null)
        {
            cachedStaffs = FindObjectsOfType<MagicStaff>();
            cacheUpdateTime = Time.time;
        }
    }

    private void Update()
    {
        if (!isDominantHand)
            return;

        // Si estamos cargando, verificar si todavía estamos sosteniendo algún bastón
        if (isCharging)
        {
            bool holdingAnyStaff = false;

            UpdateStaffsCache();
            foreach (MagicStaff staff in cachedStaffs)
            {
                if (staff != null && staff.IsHeldByDominantHand(controller))
                {
                    holdingAnyStaff = true;
                    break;
                }
            }

            if (!holdingAnyStaff)
            {
                if (showDebugMessages)
                {
                    Debug.Log($"[{Time.frameCount}] Cancelando carga porque ya no sostenemos ningún bastón");
                }
                CancelSpellCharge();
            }
        }
    }
}