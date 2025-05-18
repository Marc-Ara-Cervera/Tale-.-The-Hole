using UnityEngine;
using System.Collections;

public class VFXCircleEffect : MonoBehaviour
{
    [Header("Configuración Básica")]
    [Tooltip("Si es decorativo, aparece automáticamente")]
    public bool isDecorative = false;

    [Header("Rotación")]
    [Tooltip("Velocidad de rotación en grados/segundo")]
    public float rotationSpeed = 30f;

    [Tooltip("Velocidad al iniciar carga")]
    public float startChargeSpeed = 10f;

    [Tooltip("Velocidad máxima durante carga")]
    public float maxChargeSpeed = 180f;

    [Tooltip("Velocidad final al completar")]
    public float completeSpeed = 5f;

    [Header("Escala")]
    [Tooltip("Escala normal")]
    public float normalScale = 1.0f;

    [Tooltip("Escala aumentada al completar")]
    public float completeScale = 1.2f;

    [Tooltip("Duración de la aparición en segundos")]
    public float appearDuration = 0.5f;

    [Tooltip("Duración de la desaparición en segundos")]
    public float disappearDuration = 0.5f;

    // Variables privadas
    private Transform myTransform;
    private Vector3 originalScale;
    private float currentRotation = 0f;
    private float targetRotationSpeed;
    private float currentRotationSpeed;

    // Control de estado
    private bool isCharging = false;
    private bool isComplete = false;
    private bool isVisible = false;
    private bool isDisappearing = false;

    // Control de progreso
    private float localStartTime;
    private float totalChargeTime;
    private float expectedEndTime;
    private float appearDelay;
    private float appearTime;

    // Animación de escala
    private Vector3 targetScale;
    private float lastScaleUpdateTime;
    private float scaleSmoothness = 0.92f; // Factor de suavizado (más alto = más suave)

    // Control de transiciones
    private Coroutine scaleCoroutine;

    private void Awake()
    {
        // Cachear referencias
        myTransform = transform;
        originalScale = myTransform.localScale;

        // Inicializar invisible pero ACTIVO
        myTransform.localScale = Vector3.zero;
        targetScale = Vector3.zero;
        isVisible = false;

        // Inicializar rotación
        currentRotationSpeed = rotationSpeed;
        targetRotationSpeed = rotationSpeed;

        // Generar ángulo inicial aleatorio
        currentRotation = Random.Range(0f, 360f);
        myTransform.localRotation = Quaternion.Euler(0, currentRotation, 0);
    }

    private void Start()
    {
        // Auto-mostrar si es decorativo
        if (isDecorative)
        {
            Show();
        }
    }

    private void Update()
    {
        // Actualizar rotación si es visible
        if (isVisible)
        {
            UpdateRotation();
        }

        // Verificar si es tiempo de aparecer para círculos con delay
        if (isCharging && !isVisible && Time.time >= appearTime)
        {
            MakeVisible();
        }

        // Actualizar escala suavemente si está visible
        if (isVisible && targetScale.magnitude > 0f)
        {
            UpdateScaleSmoothly();
        }

        // Actualizar progreso de carga si está cargando y visible
        if (isCharging && isVisible && !isComplete && !isDisappearing)
        {
            UpdateChargeScaling();
        }

        // Verificar si ha desaparecido completamente
        if (isDisappearing && myTransform.localScale.magnitude < 0.01f)
        {
            CompleteDisappearing();
        }
    }

    /// <summary>
    /// Actualiza la rotación del círculo
    /// </summary>
    private void UpdateRotation()
    {
        // Interpolar hacia velocidad objetivo con umbral para evitar micro-cambios
        if (Mathf.Abs(currentRotationSpeed - targetRotationSpeed) > 0.01f)
        {
            currentRotationSpeed = Mathf.Lerp(currentRotationSpeed, targetRotationSpeed, Time.deltaTime * 3f);
        }
        else
        {
            currentRotationSpeed = targetRotationSpeed;
        }

        // Rotar en Z - usar Time.deltaTime para mantener coherencia
        currentRotation += currentRotationSpeed * Time.deltaTime;

        // Mantener en rango 0-360
        if (currentRotation >= 360f)
            currentRotation -= 360f;
        else if (currentRotation < 0f)
            currentRotation += 360f;

        // Aplicar rotación - usando Quaternion en lugar de Euler para mayor suavidad
        myTransform.localRotation = Quaternion.Euler(0, currentRotation, 0);
    }

    /// <summary>
    /// Actualiza la escala con suavizado avanzado
    /// </summary>
    private void UpdateScaleSmoothly()
    {
        // Interpolar con Spring-Dampening para animación ultra-fluida
        Vector3 currentScale = myTransform.localScale;

        // Si está desapareciendo, usar un factor de suavizado diferente para asegurar
        // que llegue a cero de manera evidente
        float smoothFactor = isDisappearing ? 0.85f : scaleSmoothness;

        // Calcular vector de velocidad de cambio
        Vector3 scaleDelta = (targetScale - currentScale) * (1f - smoothFactor);

        // Aplicar cambio con limitación de velocidad para evitar saltos
        Vector3 newScale = currentScale + scaleDelta;

        // Aplicar escala solo si hay un cambio significativo
        if ((newScale - currentScale).sqrMagnitude > 1e-8f)
        {
            myTransform.localScale = newScale;
        }

        lastScaleUpdateTime = Time.time;
    }

    /// <summary>
    /// Actualiza la escala basada en progreso de carga
    /// </summary>
    private void UpdateChargeScaling()
    {
        // Calcular tiempo desde aparición
        float elapsedSinceAppear = Time.time - appearTime;

        // Tiempo disponible para crecer
        float availableTime = expectedEndTime - appearTime;

        // Evitar división por cero
        if (availableTime <= 0.001f)
            availableTime = 0.001f;

        // Calcular progreso normalizado (0-1)
        float progress = Mathf.Clamp01(elapsedSinceAppear / availableTime);

        // Aplicar curva de easing para movimiento más natural
        float easedProgress = EaseOutCubic(progress);

        // Calcular escala objetivo basada en progreso
        targetScale = originalScale * normalScale * Mathf.Lerp(0.05f, 1.0f, easedProgress);

        // Actualizar velocidad de rotación con curva de aceleración
        // Usar curva exponencial para aceleración más natural
        float speedProgress = EaseInQuad(progress);
        targetRotationSpeed = Mathf.Lerp(
            startChargeSpeed,
            maxChargeSpeed,
            speedProgress
        ) * Mathf.Sign(rotationSpeed);

        // Verificar si la carga está completa
        if (progress >= 0.99f && !isComplete)
        {
            SetChargeComplete();
        }
    }

    /// <summary>
    /// Hace visible el círculo
    /// </summary>
    private void MakeVisible()
    {
        isVisible = true;
        isDisappearing = false;

        // Comenzar desde escala mínima pero visible
        myTransform.localScale = originalScale * normalScale * 0.05f;
        targetScale = myTransform.localScale;
    }

    /// <summary>
    /// Muestra el círculo con animación
    /// </summary>
    public void Show()
    {
        // Cancelar cualquier transición activa
        if (scaleCoroutine != null)
        {
            StopCoroutine(scaleCoroutine);
            scaleCoroutine = null;
        }

        // Hacer visible
        isVisible = true;
        isDisappearing = false;

        // Establecer escala objetivo final
        targetScale = originalScale * normalScale;

        // Configurar escala inicial si es cero
        if (myTransform.localScale.magnitude < 0.01f)
        {
            myTransform.localScale = originalScale * normalScale * 0.05f;
        }
    }

    /// <summary>
    /// Oculta el círculo con animación suave
    /// </summary>
    public void Hide()
    {
        // Cancelar cualquier corrutina activa
        if (scaleCoroutine != null)
        {
            StopCoroutine(scaleCoroutine);
            scaleCoroutine = null;
        }

        // Iniciar desaparición explícita
        StartCoroutine(DisappearSmoothly());
    }

    /// <summary>
    /// Corrutina para desaparición suave y controlada
    /// </summary>
    private IEnumerator DisappearSmoothly()
    {
        // Marcar como desapareciendo
        isDisappearing = true;

        // Guardar escala actual
        Vector3 startScale = myTransform.localScale;
        float startTime = Time.time;

        // Duración explícita
        float duration = disappearDuration;

        // Bucle de desaparición controlada
        while (Time.time < startTime + duration)
        {
            // Calcular progreso
            float progress = (Time.time - startTime) / duration;

            // Aplicar curva de suavizado
            float smoothProgress = EaseInOutCubic(progress);

            // Calcular y aplicar escala
            myTransform.localScale = Vector3.Lerp(startScale, Vector3.zero, smoothProgress);

            // Actualizar escala objetivo para el sistema de suavizado
            targetScale = myTransform.localScale;

            yield return null;
        }

        // Asegurar escala final cero
        myTransform.localScale = Vector3.zero;
        targetScale = Vector3.zero;

        // Completar desaparición
        CompleteDisappearing();
    }

    /// <summary>
    /// Finaliza el proceso de desaparición
    /// </summary>
    private void CompleteDisappearing()
    {
        // Si ya no está visible, salir
        if (!isVisible) return;

        // Actualizar estado
        isVisible = false;
        isDisappearing = false;

        // Asegurar escala cero
        myTransform.localScale = Vector3.zero;
        targetScale = Vector3.zero;

        // Desactivar objeto si no es decorativo
        if (!isDecorative)
        {
            gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Inicializa el círculo con parámetros de carga
    /// </summary>
    public void InitializeWithDelay(float delay, float totalChargeDuration)
    {
        appearDelay = delay;
        totalChargeTime = totalChargeDuration;

        // Ocultar inicialmente pero mantener ACTIVO
        myTransform.localScale = Vector3.zero;
        targetScale = Vector3.zero;
        isVisible = false;
        isDisappearing = false;
    }

    /// <summary>
    /// Inicia el efecto de carga
    /// </summary>
    public void StartChargeEffect()
    {
        if (isDecorative) return;

        // Inicializar estado
        isCharging = true;
        isComplete = false;
        isDisappearing = false;

        // Mantener activo pero invisible si hay delay
        myTransform.localScale = Vector3.zero;
        targetScale = Vector3.zero;
        isVisible = false;

        // Calcular tiempos de referencia
        localStartTime = Time.time;
        appearTime = localStartTime + appearDelay;
        expectedEndTime = localStartTime + totalChargeTime;

        // Configurar velocidad inicial
        targetRotationSpeed = startChargeSpeed * Mathf.Sign(rotationSpeed);

        // Comenzar inmediatamente si no hay delay
        if (appearDelay <= 0.001f)
        {
            MakeVisible();
        }
    }

    /// <summary>
    /// Actualiza el progreso de carga global
    /// </summary>
    public void UpdateProgress(float globalProgress)
    {
        if (isDecorative || !isCharging || isComplete || isDisappearing) return;

        // Verificar si la carga está completa según progreso global
        if (globalProgress >= 0.99f && isVisible && !isComplete)
        {
            SetChargeComplete();
        }
    }

    /// <summary>
    /// Establece el estado de carga completa
    /// </summary>
    public void SetChargeComplete()
    {
        if (isDecorative || isComplete || !isVisible || isDisappearing) return;

        // Actualizar estado
        isComplete = true;

        // Configurar velocidad final
        targetRotationSpeed = completeSpeed * Mathf.Sign(rotationSpeed);

        // Cancelar cualquier corrutina actual
        if (scaleCoroutine != null)
        {
            StopCoroutine(scaleCoroutine);
        }

        // Iniciar pulso visual
        scaleCoroutine = StartCoroutine(CompletePulseSequence());
    }

    /// <summary>
    /// Secuencia de pulso al completar carga
    /// </summary>
    private IEnumerator CompletePulseSequence()
    {
        // Efecto de pulso mejorado
        float pulseDuration = 0.7f;
        float startTime = Time.time;

        // Guardar escala actual antes del pulso
        Vector3 startScale = myTransform.localScale;
        Vector3 fullScale = originalScale * normalScale;
        Vector3 maxScale = fullScale * completeScale;
        Vector3 endScale = fullScale * 1.05f;

        while (Time.time < startTime + pulseDuration)
        {
            // Normalizar tiempo
            float elapsed = Time.time - startTime;
            float t = elapsed / pulseDuration;

            // Primera mitad: crecer hasta escala máxima
            if (t < 0.3f)
            {
                float growProgress = t / 0.3f;
                myTransform.localScale = Vector3.Lerp(startScale, maxScale, EaseOutCubic(growProgress));
                targetScale = myTransform.localScale;
            }
            // Segunda mitad: reducir ligeramente
            else
            {
                float shrinkProgress = (t - 0.3f) / 0.7f;
                myTransform.localScale = Vector3.Lerp(maxScale, endScale, EaseInOutCubic(shrinkProgress));
                targetScale = myTransform.localScale;
            }

            yield return null;
        }

        // Establecer escala final
        myTransform.localScale = endScale;
        targetScale = endScale;
    }

    // Funciones de easing para animación más natural
    private float EaseOutCubic(float t)
    {
        return 1 - Mathf.Pow(1 - t, 3);
    }

    private float EaseInQuad(float t)
    {
        return t * t;
    }

    private float EaseInOutCubic(float t)
    {
        return t < 0.5 ? 4 * t * t * t : 1 - Mathf.Pow(-2 * t + 2, 3) / 2;
    }
}