using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.InputSystem;

public class SpellCastController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private XRBaseController controller;

    [Header("Configuraci�n de Input")]
    [SerializeField] private InputActionReference castSpellAction;

    [Header("Debug")]
    [SerializeField] private bool showDebugMessages = false;

    // Cache de bastones
    private MagicStaff[] cachedStaffs;
    private float cacheUpdateTime = 0f;
    private float cacheLifetime = 1f;

    // Estado de carga
    private bool isCharging = false;
    private float chargeStartTime = 0f;

    private void Awake()
    {
        // Obtener XRBaseController si no est� asignado
        if (controller == null)
        {
            controller = GetComponent<XRBaseController>();
            if (controller == null)
            {
                Debug.LogError("No se encontr� un XRBaseController en el GameObject. Por favor, asigne uno manualmente.");
            }
        }
    }

    private void OnEnable()
    {
        if (castSpellAction != null)
        {
            castSpellAction.action.Enable();

            // Suscribirse tanto al presionar como al soltar
            castSpellAction.action.started += OnSpellChargeStarted;
            castSpellAction.action.canceled += OnSpellChargeEnded;
        }
        else
        {
            Debug.LogWarning("No se ha asignado una acci�n de input para lanzar hechizos.");
        }
    }

    private void OnDisable()
    {
        if (castSpellAction != null)
        {
            castSpellAction.action.Disable();

            // Desuscribirse de ambos eventos
            castSpellAction.action.started -= OnSpellChargeStarted;
            castSpellAction.action.canceled -= OnSpellChargeEnded;
        }

        // Asegurarse de cancelar cualquier carga en progreso
        if (isCharging)
        {
            CancelSpellCharge();
        }
    }

    /// <summary>
    /// Llamado cuando el bot�n de hechizo comienza a ser presionado
    /// </summary>
    private void OnSpellChargeStarted(InputAction.CallbackContext context)
    {
        if (showDebugMessages)
        {
            Debug.Log($"Comenzando carga de hechizo en controlador: {controller.name}");
        }

        // Iniciar la carga
        isCharging = true;
        chargeStartTime = Time.time;

        // Notificar a los bastones para iniciar efectos visuales de carga
        UpdateStaffsCache();
        foreach (MagicStaff staff in cachedStaffs)
        {
            if (staff != null)
            {
                staff.StartCharging(controller);
            }
        }
    }

    /// <summary>
    /// Llamado cuando el bot�n de hechizo es liberado
    /// </summary>
    private void OnSpellChargeEnded(InputAction.CallbackContext context)
    {
        if (!isCharging)
            return;

        float chargeTime = Time.time - chargeStartTime;

        if (showDebugMessages)
        {
            Debug.Log($"Finalizando carga de hechizo en controlador: {controller.name}. Tiempo de carga: {chargeTime}s");
        }

        // Resetear estado de carga
        isCharging = false;

        // Intentar lanzar el hechizo (la verificaci�n del tiempo m�nimo ocurrir� en el bast�n)
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
    /// Cancela la carga actual si el jugador suelta el bast�n mientras carga
    /// </summary>
    public void CancelSpellCharge()
    {
        if (!isCharging)
            return;

        if (showDebugMessages)
        {
            Debug.Log($"Carga de hechizo cancelada en controlador: {controller.name}");
        }

        // Resetear estado de carga
        isCharging = false;

        // Notificar a los bastones para cancelar efectos visuales
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
    /// Actualiza la cach� de bastones
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
        // Si estamos en modo de carga pero el jugador ha soltado el bast�n, cancelar la carga
        if (isCharging)
        {
            // Verificar si alg�n bast�n est� siendo sostenido por este controlador
            bool holdingAnyStaff = false;

            UpdateStaffsCache();
            foreach (MagicStaff staff in cachedStaffs)
            {
                if (staff != null && staff.IsHeldBy(controller))
                {
                    holdingAnyStaff = true;
                    break;
                }
            }

            // Si no est� sosteniendo ning�n bast�n, cancelar la carga
            if (!holdingAnyStaff)
            {
                CancelSpellCharge();
            }
        }
    }
}