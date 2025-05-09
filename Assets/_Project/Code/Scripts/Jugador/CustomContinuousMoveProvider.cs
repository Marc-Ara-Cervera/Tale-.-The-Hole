using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;

public class CustomContinuousMoveProvider : ActionBasedContinuousMoveProvider
{
    [SerializeField]
    [Tooltip("The Input System Action that will be used to activate sprint. Must be a Button Control.")]
    InputActionProperty m_SprintAction = new InputActionProperty(new InputAction("Sprint", expectedControlType: "Button"));

    [SerializeField]
    [Tooltip("Multiplier applied to movement speed when sprint is active.")]
    float m_SprintSpeedMultiplier = 2.0f;

    // A�adir esto para debug
    [SerializeField]
    bool m_DebugMode = true;

    private Vector3 m_PreviousMovement = Vector3.zero;
    private StaminaStat m_StaminaStat;
    private bool m_WasSprinting = false;
    private float m_DefaultMoveSpeed;

    protected void Start()
    {
        // Guardar la velocidad original para restaurarla cuando no estemos en sprint
        m_DefaultMoveSpeed = moveSpeed;

        // Buscar el StaminaStat
        FindStaminaStat();

        // Habilitar la acci�n de sprint manualmente
        m_SprintAction.action?.Enable();

        if (m_DebugMode)
            Debug.Log("CustomContinuousMoveProvider inicializado. Velocidad base: " + m_DefaultMoveSpeed);
    }

    private void FindStaminaStat()
    {
        var statsManager = FindObjectOfType<PlayerStatsManager>();
        if (statsManager != null)
        {
            m_StaminaStat = statsManager.Stamina;
            if (m_DebugMode)
                Debug.Log("StaminaStat encontrado");
        }
        else
        {
            Debug.LogWarning("No se encontr� PlayerStatsManager. El sprint no utilizar� estamina.");
        }
    }

    // En lugar de sobrescribir OnEnable/OnDisable, usamos OnDestroy para limpiar
    protected void OnDestroy()
    {
        if (m_SprintAction.action != null)
            m_SprintAction.action.Disable();
    }

    // M�todo para verificar si el bot�n de sprint est� presionado (m�s detallado)
    private bool IsSprintButtonPressed()
    {
        if (m_SprintAction.action == null)
            return false;

        // Probar diferentes m�todos de lectura
        float value = m_SprintAction.action.ReadValue<float>();
        bool isPressed = value > 0.5f || m_SprintAction.action.IsPressed();

        if (m_DebugMode && isPressed)
            Debug.Log($"Bot�n de sprint detectado! Valor: {value}, IsPressed: {m_SprintAction.action.IsPressed()}");

        return isPressed;
    }

    protected override Vector3 ComputeDesiredMove(Vector2 input)
    {
        // Verificar el estado del sprint antes de calcular el movimiento
        UpdateSprintState();

        // Obtiene el valor base de movimiento
        Vector3 movement = base.ComputeDesiredMove(input);

        // Suavizado para velocidades altas
        if (movement.magnitude > 0.8f)
        {
            movement = Vector3.Lerp(m_PreviousMovement, movement, Time.deltaTime * 8f);
        }

        // Guardar el movimiento para el pr�ximo frame
        m_PreviousMovement = movement;

        return movement;
    }

    // Separar la l�gica del sprint para mayor claridad
    private void UpdateSprintState()
    {
        // Verificar si la tecla est� presionada
        bool sprintPressed = IsSprintButtonPressed();

        // Verificar si podemos sprintar (estamina suficiente)
        bool canSprint = m_StaminaStat != null && m_StaminaStat.CanSprint();

        // Determinar el estado del sprint
        bool shouldSprint = sprintPressed && canSprint;

        // Si debemos sprintar y no est�bamos sprintando antes
        if (shouldSprint && !m_WasSprinting)
        {
            // Activar sprint
            moveSpeed = m_DefaultMoveSpeed * m_SprintSpeedMultiplier;

            if (m_StaminaStat != null)
                m_StaminaStat.StartSprinting();

            m_WasSprinting = true;

            if (m_DebugMode)
                Debug.Log($"Sprint ACTIVADO! Velocidad: {moveSpeed}, Estamina: {m_StaminaStat?.GetCurrentValue() ?? 0}");
        }
        // Si no debemos sprintar pero est�bamos sprintando antes
        else if (!shouldSprint && m_WasSprinting)
        {
            // Desactivar sprint
            moveSpeed = m_DefaultMoveSpeed;

            if (m_StaminaStat != null)
                m_StaminaStat.StopSprinting();

            m_WasSprinting = false;

            if (m_DebugMode)
                Debug.Log($"Sprint DESACTIVADO! Velocidad: {moveSpeed}, Estamina: {m_StaminaStat?.GetCurrentValue() ?? 0}");
        }

        // Debug adicional
        if (m_DebugMode && sprintPressed)
        {
            Debug.Log($"Sprint Presionado: {sprintPressed}, Puede Sprintar: {canSprint}, Est� Sprintando: {m_WasSprinting}");
        }
    }
}