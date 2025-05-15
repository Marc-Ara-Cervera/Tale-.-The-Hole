using UnityEngine;
using UnityEngine.Events;
using System.Collections;

/// <summary>
/// Sistema de efectos visuales para círculos mágicos.
/// Funciona tanto para decoración de escenarios como para efectos de carga.
/// </summary>
public class VFXCircleEffect : MonoBehaviour
{
    [System.Serializable]
    public enum RotationAxis
    {
        X,
        Y,
        Z
    }

    #region Configuración General
    [Header("Modo de Operación")]
    [Tooltip("Si está activo, el círculo funciona como decoración y aparece automáticamente")]
    [SerializeField] public bool isDecorative = false;

    [Tooltip("Retraso antes de aparecer (solo en modo decorativo)")]
    [Range(0f, 5f)]
    [SerializeField] private float initialDelay = 0f;
    #endregion

    #region Configuración de Rotación
    [Header("Rotación")]
    [Tooltip("Eje alrededor del cual girará el objeto")]
    [SerializeField] private RotationAxis axis = RotationAxis.Z;

    [Tooltip("Velocidad de rotación en grados por segundo")]
    [SerializeField] public float rotationSpeed = 30f;

    [Tooltip("Velocidad de rotación mínima (al inicio de la carga)")]
    [Range(5f, 90f)]
    [SerializeField] public float minChargeRotationSpeed = 10f;

    [Tooltip("Velocidad de rotación máxima (cerca del final de la carga)")]
    [Range(90f, 360f)]
    [SerializeField] public float maxChargeRotationSpeed = 180f;

    [Tooltip("Velocidad de rotación cuando la carga está completa")]
    [Range(0f, 30f)]
    [SerializeField] public float completeRotationSpeed = 5f;

    [Tooltip("Velocidad de reducción de rotación")]
    [Range(1f, 10f)]
    [SerializeField] private float rotationDecelerationSpeed = 3f;

    [Tooltip("Invertir dirección de rotación")]
    [SerializeField] private bool counterClockwise = false;

    [Tooltip("Utilizar velocidad fluctuante para efecto más orgánico")]
    [SerializeField] private bool useSpeedFluctuation = false;

    [Tooltip("Intensidad de la fluctuación")]
    [Range(0f, 0.5f)]
    [SerializeField] private float fluctuationIntensity = 0.2f;

    [Tooltip("Velocidad de fluctuación")]
    [Range(0.1f, 3f)]
    [SerializeField] private float fluctuationSpeed = 0.5f;
    #endregion

    #region Configuración de Escala
    [Header("Escala y Transiciones")]
    [Tooltip("Escala inicial al aparecer")]
    [Range(0.01f, 0.5f)]
    [SerializeField] private float initialScale = 0.1f;

    [Tooltip("Escala final")]
    [Range(0.5f, 2.0f)]
    [SerializeField] private float finalScale = 1.0f;

    [Tooltip("Escala al completar la carga")]
    [Range(1.0f, 2.0f)]
    [SerializeField] private float completeScale = 1.2f;

    [Tooltip("Velocidad de aparición")]
    [Range(0.5f, 5.0f)]
    [SerializeField] private float appearSpeed = 2.0f;

    [Tooltip("Velocidad de desaparición")]
    [Range(0.5f, 5.0f)]
    [SerializeField] private float disappearSpeed = 3.0f;

    [Tooltip("Usar efecto de 'respiración' (escala pulsante)")]
    [SerializeField] private bool useBreathingEffect = false;

    [Tooltip("Intensidad del efecto de respiración")]
    [Range(0f, 0.3f)]
    [SerializeField] private float breathingIntensity = 0.05f;

    [Tooltip("Velocidad del efecto de respiración")]
    [Range(0.1f, 2f)]
    [SerializeField] private float breathingSpeed = 0.5f;
    #endregion

    #region Eventos
    [Header("Eventos")]
    public UnityEvent OnAppearComplete;
    public UnityEvent OnChargeComplete;
    public UnityEvent OnDisappearComplete;
    #endregion

    // Estados
    private enum CircleState { Inactive, Appearing, Active, Charging, ChargeComplete, Disappearing }
    private CircleState currentState = CircleState.Inactive;

    // Variables internas
    private Vector3 originalScale;
    private float currentAngle = 0f;
    private float currentRotationSpeed;
    private float targetRotationSpeed;
    private float chargeProgress = 0f;
    private bool initialized = false;

    // Para transiciones
    private float transitionStartTime;
    private Vector3 transitionStartScale;
    private Vector3 transitionTargetScale;
    private float transitionDuration;
    private bool inTransition = false;

    private void Awake()
    {
        // Guardar escala original
        originalScale = transform.localScale;

        // Inicializar rotación
        float direction = counterClockwise ? -1f : 1f;
        currentRotationSpeed = rotationSpeed * direction;
        targetRotationSpeed = rotationSpeed * direction;

        // Inicializar con escala cero
        transform.localScale = Vector3.zero;
    }

    private void Start()
    {
        // Generar ángulo inicial aleatorio
        currentAngle = Random.Range(0f, 360f);

        // Inicializar
        initialized = true;

        // Si es decorativo, iniciar aparición automáticamente con retraso
        if (isDecorative)
        {
            StartCoroutine(AutoShow());
        }
    }

    private IEnumerator AutoShow()
    {
        // Esperar el retraso inicial
        if (initialDelay > 0)
        {
            yield return new WaitForSeconds(initialDelay);
        }

        // Mostrar el círculo
        Show();
    }

    private void Update()
    {
        if (!initialized) return;

        // Actualizar escala durante transiciones
        if (inTransition)
        {
            UpdateScaleTransition();
        }
        else if (currentState == CircleState.Active && useBreathingEffect)
        {
            // Aplicar efecto de respiración si estamos en modo activo
            ApplyBreathingEffect();
        }

        // Actualizar rotación
        UpdateRotation();
    }

    // Método para manejar transiciones de escala
    private void UpdateScaleTransition()
    {
        float elapsed = Time.time - transitionStartTime;

        if (elapsed < transitionDuration)
        {
            // Calcular progreso con suavizado mejorado (ease in/out)
            float progress = elapsed / transitionDuration;
            progress = EaseInOutCubic(progress);

            transform.localScale = Vector3.Lerp(transitionStartScale, transitionTargetScale, progress);
        }
        else
        {
            // Completar transición
            transform.localScale = transitionTargetScale;
            inTransition = false;

            // Manejar eventos según el estado
            if (currentState == CircleState.Appearing)
            {
                currentState = currentState = chargeProgress > 0 ? CircleState.Charging : CircleState.Active;
                OnAppearComplete?.Invoke();
            }
            else if (currentState == CircleState.Disappearing)
            {
                currentState = CircleState.Inactive;
                OnDisappearComplete?.Invoke();

                // Opcionalmente desactivar si no es decorativo
                if (!isDecorative)
                {
                    gameObject.SetActive(false);
                }
            }
        }
    }

    // Función de suavizado cúbico (ease in/out)
    private float EaseInOutCubic(float t)
    {
        return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
    }

    // Aplica el efecto de "respiración" a la escala
    private void ApplyBreathingEffect()
    {
        if (!useBreathingEffect) return;

        // Usar seno para efecto ondulante
        float breathFactor = 1f + Mathf.Sin(Time.time * breathingSpeed * Mathf.PI) * breathingIntensity;

        // Aplicar a la escala
        transform.localScale = originalScale * finalScale * breathFactor;
    }

    private void UpdateRotation()
    {
        // Aplicar fluctuación si está activa
        float speedModifier = 1f;
        if (useSpeedFluctuation && (currentState == CircleState.Active || isDecorative))
        {
            speedModifier = 1f + Mathf.Sin(Time.time * fluctuationSpeed) * fluctuationIntensity;
        }

        // Interpolar velocidad hacia objetivo
        if (currentState == CircleState.ChargeComplete)
        {
            currentRotationSpeed = Mathf.Lerp(currentRotationSpeed, targetRotationSpeed,
                                            Time.deltaTime * rotationDecelerationSpeed);
        }
        else
        {
            currentRotationSpeed = Mathf.Lerp(currentRotationSpeed, targetRotationSpeed * speedModifier,
                                            Time.deltaTime * 5f);
        }

        // Actualizar ángulo
        currentAngle += currentRotationSpeed * Time.deltaTime;

        // Mantener ángulo en rango 0-360
        if (currentAngle >= 360f)
            currentAngle -= 360f;
        else if (currentAngle < 0f)
            currentAngle += 360f;

        // Aplicar rotación según eje
        Vector3 rotationAngles = Vector3.zero;
        switch (axis)
        {
            case RotationAxis.X:
                rotationAngles = new Vector3(currentAngle, 0, 0);
                break;
            case RotationAxis.Y:
                rotationAngles = new Vector3(0, currentAngle, 0);
                break;
            case RotationAxis.Z:
                rotationAngles = new Vector3(0, 0, currentAngle);
                break;
        }

        transform.localRotation = Quaternion.Euler(rotationAngles);
    }

    #region Métodos Públicos

    /// <summary>
    /// Inicia la aparición del círculo
    /// </summary>
    public void Show()
    {
        // Asegurar que estamos activos
        gameObject.SetActive(true);

        // Configurar transición
        transitionStartTime = Time.time;
        transitionStartScale = Vector3.zero;
        transitionTargetScale = originalScale * finalScale;
        transitionDuration = 1f / appearSpeed;
        inTransition = true;

        // Actualizar estado
        currentState = CircleState.Appearing;

        // Configurar rotación
        float direction = counterClockwise ? -1f : 1f;
        targetRotationSpeed = rotationSpeed * direction;

        if (isDecorative)
        {
            currentRotationSpeed = targetRotationSpeed;
        }
    }

    /// <summary>
    /// Comienza el efecto de carga
    /// </summary>
    public void StartChargeEffect()
    {
        if (isDecorative) return; // No aplicar en modo decorativo

        // Asegurar que estamos activos
        gameObject.SetActive(true);

        // Configurar transición
        transitionStartTime = Time.time;
        transitionStartScale = Vector3.zero;
        transitionTargetScale = originalScale * initialScale;
        transitionDuration = 1f / appearSpeed;
        inTransition = true;

        // Actualizar estado
        currentState = CircleState.Appearing;
        chargeProgress = 0f;

        // Configurar rotación inicial
        float direction = counterClockwise ? -1f : 1f;
        targetRotationSpeed = minChargeRotationSpeed * direction;
        currentRotationSpeed = minChargeRotationSpeed * direction;
    }

    /// <summary>
    /// Actualiza el progreso de carga
    /// </summary>
    public void UpdateProgress(float progress)
    {
        if (isDecorative) return; // No aplicar en modo decorativo

        chargeProgress = Mathf.Clamp01(progress);

        // Si aún no estamos en carga, salir
        if (currentState != CircleState.Charging &&
            currentState != CircleState.Active &&
            currentState != CircleState.Appearing)
            return;

        // Actualizar estado si es necesario
        if (currentState == CircleState.Active && progress > 0)
        {
            currentState = CircleState.Charging;
        }

        // Actualizar velocidad según progreso (curva cuadrática)
        float direction = counterClockwise ? -1f : 1f;
        targetRotationSpeed = Mathf.Lerp(minChargeRotationSpeed,
                                        maxChargeRotationSpeed,
                                        chargeProgress * chargeProgress) * direction;

        // Actualizar escala si no estamos en transición
        if (!inTransition)
        {
            Vector3 newScale = Vector3.Lerp(
                originalScale * initialScale,
                originalScale * finalScale,
                chargeProgress);

            transform.localScale = newScale;
        }

        // Comprobar si completamos la carga
        if (progress >= 0.99f && currentState == CircleState.Charging)
        {
            SetChargeComplete();
        }
    }

    /// <summary>
    /// Activa el efecto de carga completa
    /// </summary>
    public void SetChargeComplete()
    {
        if (isDecorative || currentState == CircleState.ChargeComplete)
            return;

        // Configurar transición
        transitionStartTime = Time.time;
        transitionStartScale = transform.localScale;
        transitionTargetScale = originalScale * completeScale;
        transitionDuration = 0.5f;
        inTransition = true;

        // Actualizar estado
        currentState = CircleState.ChargeComplete;

        // Configurar rotación final
        float direction = counterClockwise ? -1f : 1f;
        targetRotationSpeed = completeRotationSpeed * direction;

        // Disparar evento
        OnChargeComplete?.Invoke();
    }

    /// <summary>
    /// Inicia la desaparición
    /// </summary>
    public void Hide()
    {
        // Configurar transición
        transitionStartTime = Time.time;
        transitionStartScale = transform.localScale;
        transitionTargetScale = Vector3.zero;
        transitionDuration = 1f / disappearSpeed;
        inTransition = true;

        // Actualizar estado
        currentState = CircleState.Disappearing;
    }

    /// <summary>
    /// Configura parámetros de rotación
    /// </summary>
    public void SetRotationDirection(bool counterClockwiseDir)
    {
        this.counterClockwise = counterClockwiseDir;
        float direction = counterClockwiseDir ? -1f : 1f;

        // Mantener dirección pero invertir signo
        rotationSpeed = Mathf.Abs(rotationSpeed) * direction;
        minChargeRotationSpeed = Mathf.Abs(minChargeRotationSpeed) * direction;
        maxChargeRotationSpeed = Mathf.Abs(maxChargeRotationSpeed) * direction;
        completeRotationSpeed = Mathf.Abs(completeRotationSpeed) * direction;

        // Actualizar velocidad actual
        targetRotationSpeed = Mathf.Abs(targetRotationSpeed) * direction;
    }

    /// <summary>
    /// Establece la velocidad mínima de rotación
    /// </summary>
    public void SetMinRotationSpeed(float speed)
    {
        float direction = counterClockwise ? -1f : 1f;
        minChargeRotationSpeed = Mathf.Abs(speed) * direction;
    }

    /// <summary>
    /// Establece la velocidad máxima de rotación
    /// </summary>
    public void SetMaxRotationSpeed(float speed)
    {
        float direction = counterClockwise ? -1f : 1f;
        maxChargeRotationSpeed = Mathf.Abs(speed) * direction;
    }

    /// <summary>
    /// Establece la velocidad normal de rotación
    /// </summary>
    public void SetNormalRotationSpeed(float speed)
    {
        float direction = counterClockwise ? -1f : 1f;
        rotationSpeed = Mathf.Abs(speed) * direction;

        // Actualizar velocidad objetivo si estamos en modo activo
        if (currentState == CircleState.Active || isDecorative)
        {
            targetRotationSpeed = rotationSpeed;
        }
    }

    #endregion
}