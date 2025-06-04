using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Tipos de daño elemental para las explosiones
/// </summary>
public enum DamageType
{
    Physical,
    Fire,
    Ice,
    Lightning,
    Poison,
    Holy,
    Dark
}

/// <summary>
/// Información sobre el origen de la explosión
/// </summary>
[System.Serializable]
public struct ExplosionSource
{
    public GameObject instigator;       // Quien causó la explosión
    public float baseDamage;           // Daño base de la habilidad
    public DamageType damageType;      // Tipo de daño
    public HashSet<GameObject> excludeTargets; // Objetivos a excluir (ej: ya dañados por proyectil)
    
    public ExplosionSource(GameObject source, float damage, DamageType type = DamageType.Physical)
    {
        instigator = source;
        baseDamage = damage;
        damageType = type;
        excludeTargets = new HashSet<GameObject>();
    }
}

/// <summary>
/// Sistema de explosiones que aplica daño en área con falloff por distancia
/// </summary>
public class Explosion : MonoBehaviour
{
    [Header("---- CONFIGURACIÓN DE EXPLOSIÓN ----")]
    [SerializeField, Tooltip("Radio de la explosión en metros")]
    private float explosionRadius = 5f;
    
    [SerializeField, Tooltip("Daño por defecto si no viene de una habilidad")]
    private float defaultDamage = 50f;
    
    [SerializeField, Tooltip("Tipo de daño por defecto")]
    private DamageType defaultDamageType = DamageType.Physical;
    
    [Header("---- CONFIGURACIÓN DE FALLOFF ----")]
    [SerializeField, Tooltip("Curva de daño por distancia (0 = centro, 1 = borde)")]
    private AnimationCurve damageFalloffCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0.2f);
    
    [SerializeField, Tooltip("¿El daño mínimo en el borde es 0?")]
    private bool zeroAtEdge = false;
    
    [SerializeField, Tooltip("Daño mínimo garantizado (% del daño base)"), Range(0f, 1f)]
    private float minimumDamagePercent = 0.1f;
    
    [Header("---- FÍSICA Y FUERZAS ----")]
    [SerializeField, Tooltip("Fuerza de empuje")]
    private float knockbackForce = 10f;
    
    [SerializeField, Tooltip("Curva de fuerza por distancia")]
    private AnimationCurve knockbackFalloffCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0.3f);
    
    [SerializeField, Tooltip("¿Levantar objetivos del suelo?")]
    private bool liftTargets = true;
    
    [SerializeField, Tooltip("Multiplicador de fuerza vertical"), Range(0f, 2f)]
    private float verticalForceMultiplier = 0.5f;
    
    [Header("---- TARGETING ----")]
    [SerializeField, Tooltip("Capas que puede afectar la explosión")]
    private LayerMask affectedLayers = -1;
    
    [SerializeField, Tooltip("¿Puede dañar aliados?")]
    private bool canDamageAllies = false;
    
    [SerializeField, Tooltip("¿Requiere línea de visión?")]
    private bool requireLineOfSight = true;
    
    [SerializeField, Tooltip("Capas que bloquean la explosión")]
    private LayerMask obstructionLayers = -1;
    
    [Header("---- EFECTOS ADICIONALES ----")]
    [SerializeField, Tooltip("Duración del aturdimiento"), Range(0f, 5f)]
    private float stunDuration = 0f;
    
    [SerializeField, Tooltip("Probabilidad de efecto crítico"), Range(0f, 1f)]
    private float criticalChance = 0.1f;
    
    [SerializeField, Tooltip("Multiplicador de daño crítico")]
    private float criticalMultiplier = 2f;
    
    [Header("---- AUDIO ----")]
    [SerializeField, Tooltip("Sonido de la explosión")]
    private AudioClip explosionSound;
    
    [SerializeField, Tooltip("Volumen del sonido"), Range(0f, 1f)]
    private float soundVolume = 1f;
    
    [SerializeField, Tooltip("Distancia mínima del sonido")]
    private float soundMinDistance = 1f;
    
    [SerializeField, Tooltip("Distancia máxima del sonido")]
    private float soundMaxDistance = 50f;
    
    [SerializeField, Tooltip("Tipo de rolloff del sonido")]
    private AudioRolloffMode soundRolloffMode = AudioRolloffMode.Logarithmic;
    
    [SerializeField, Tooltip("Prioridad del sonido (0 = más alta)"), Range(0, 256)]
    private int soundPriority = 128;
    
    [SerializeField, Tooltip("Variación aleatoria del pitch"), Range(0f, 0.5f)]
    private float pitchVariation = 0.1f;
    
    [Header("---- DEBUG ----")]
    [SerializeField] private bool showDebugInfo = true;
    [SerializeField] private Color debugColor = new Color(1f, 0.5f, 0f, 0.3f);
    
    // Estado interno
    private ExplosionSource explosionSource;
    private bool hasExploded = false;
    private HashSet<GameObject> damagedTargets = new HashSet<GameObject>();
    
    #region Inicialización
    
    /// <summary>
    /// Inicializa la explosión con información de la fuente
    /// </summary>
    public void Initialize(ExplosionSource source)
    {
        explosionSource = source;
        
        // Si hay objetivos a excluir, añadirlos a la lista
        if (source.excludeTargets != null)
        {
            foreach (var target in source.excludeTargets)
            {
                damagedTargets.Add(target);
            }
        }
    }
    
    /// <summary>
    /// Inicialización simple con solo daño
    /// </summary>
    public void Initialize(float damage, GameObject instigator = null)
    {
        explosionSource = new ExplosionSource(instigator, damage, defaultDamageType);
    }
    
    private void Start()
    {
        // Explotar inmediatamente
        Explode();
        
        // Auto-destruir después de un tiempo para limpiar
        Destroy(gameObject, 0.5f);
    }
    
    #endregion
    
    #region Lógica de Explosión
    
    /// <summary>
    /// Ejecuta la explosión aplicando efectos a todos los objetivos en rango
    /// </summary>
    private void Explode()
    {
        if (hasExploded) return;
        hasExploded = true;
        
        Vector3 explosionCenter = transform.position;
        
        // Reproducir sonido de explosión
        PlayExplosionSound(explosionCenter);
        
        // Obtener el daño base
        float baseDamage = explosionSource.baseDamage > 0 ? explosionSource.baseDamage : defaultDamage;
        
        if (showDebugInfo)
        {
            Debug.Log($"[Explosion] Explotando en {explosionCenter} con {baseDamage} de daño base, radio {explosionRadius}m");
        }
        
        // Encontrar todos los objetivos potenciales
        Collider[] potentialTargets = Physics.OverlapSphere(explosionCenter, explosionRadius, affectedLayers);
        
        // Procesar cada objetivo
        foreach (Collider col in potentialTargets)
        {
            ProcessTarget(col, explosionCenter, baseDamage);
        }
        
        // Notificar estadísticas finales
        if (showDebugInfo)
        {
            Debug.Log($"[Explosion] Procesados {potentialTargets.Length} objetivos, {damagedTargets.Count} dañados");
        }
    }
    
    /// <summary>
    /// Procesa un objetivo individual
    /// </summary>
    private void ProcessTarget(Collider target, Vector3 explosionCenter, float baseDamage)
    {
        GameObject targetObject = target.gameObject;
        
        // Verificar si ya fue dañado
        if (damagedTargets.Contains(targetObject))
        {
            if (showDebugInfo)
                Debug.Log($"[Explosion] {targetObject.name} ya fue dañado, ignorando");
            return;
        }
        
        // Verificar línea de visión si es necesario
        if (requireLineOfSight && !HasLineOfSight(explosionCenter, target))
        {
            if (showDebugInfo)
                Debug.Log($"[Explosion] {targetObject.name} bloqueado por obstáculo");
            return;
        }
        
        // Verificar si es un objetivo válido
        var damageable = target.GetComponent<iDañable>();
        if (damageable == null)
        {
            // Intentar aplicar física aunque no sea dañable
            ApplyPhysicsForce(target, explosionCenter);
            return;
        }
        
        // Verificar alianza si es necesario
        if (!canDamageAllies && !IsValidTarget(targetObject))
        {
            if (showDebugInfo)
                Debug.Log($"[Explosion] {targetObject.name} es aliado, ignorando");
            return;
        }
        
        // Calcular distancia y daño
        float distance = Vector3.Distance(explosionCenter, target.transform.position);
        float normalizedDistance = Mathf.Clamp01(distance / explosionRadius);
        
        // Calcular daño con falloff
        float damageMultiplier = damageFalloffCurve.Evaluate(normalizedDistance);
        
        // Aplicar daño mínimo si no es zero at edge
        if (!zeroAtEdge)
        {
            damageMultiplier = Mathf.Max(damageMultiplier, minimumDamagePercent);
        }
        
        float finalDamage = baseDamage * damageMultiplier;
        
        // Verificar crítico
        if (Random.value < criticalChance)
        {
            finalDamage *= criticalMultiplier;
            if (showDebugInfo)
                Debug.Log($"[Explosion] ¡Golpe crítico en {targetObject.name}!");
        }
        
        // Aplicar el daño
        damageable.TakeDamage(finalDamage, explosionSource.instigator);
        damagedTargets.Add(targetObject);
        
        // Aplicar efectos adicionales
        ApplyAdditionalEffects(target, explosionCenter, normalizedDistance);
        
        if (showDebugInfo)
        {
            Debug.Log($"[Explosion] {targetObject.name} recibió {finalDamage:F1} daño (distancia: {distance:F1}m, multiplicador: {damageMultiplier:F2})");
        }
    }
    
    #endregion
    
    #region Efectos Adicionales
    
    /// <summary>
    /// Aplica efectos adicionales como knockback y stun
    /// </summary>
    private void ApplyAdditionalEffects(Collider target, Vector3 explosionCenter, float normalizedDistance)
    {
        // Aplicar fuerza física
        ApplyPhysicsForce(target, explosionCenter);
        
        // Aplicar stun si está configurado
        if (stunDuration > 0)
        {
            ApplyStun(target.gameObject, stunDuration);
        }
        
        // Aquí puedes añadir más efectos como:
        // - Quemar (DoT)
        // - Congelar
        // - Electrocutar
        // - Envenenar
        // etc.
    }
    
    /// <summary>
    /// Aplica fuerza de knockback
    /// </summary>
    private void ApplyPhysicsForce(Collider target, Vector3 explosionCenter)
    {
        if (knockbackForce <= 0) return;
        
        Rigidbody rb = target.GetComponent<Rigidbody>();
        if (rb == null || rb.isKinematic) return;
        
        // Calcular dirección y distancia
        Vector3 direction = (target.transform.position - explosionCenter).normalized;
        float distance = Vector3.Distance(explosionCenter, target.transform.position);
        float normalizedDistance = Mathf.Clamp01(distance / explosionRadius);
        
        // Calcular fuerza con falloff
        float forceMultiplier = knockbackFalloffCurve.Evaluate(normalizedDistance);
        float finalForce = knockbackForce * forceMultiplier;
        
        // Añadir componente vertical si está habilitado
        if (liftTargets)
        {
            direction.y = verticalForceMultiplier;
            direction.Normalize();
        }
        
        // Aplicar la fuerza
        rb.AddForce(direction * finalForce, ForceMode.Impulse);
        
        if (showDebugInfo)
        {
            Debug.Log($"[Explosion] Knockback aplicado a {target.name}: {finalForce:F1}N");
        }
    }
    
    /// <summary>
    /// Aplica efecto de aturdimiento
    /// </summary>
    private void ApplyStun(GameObject target, float duration)
    {
        // Aquí implementarías tu sistema de stun
        // Por ejemplo, buscar un componente que maneje estados
        
        if (showDebugInfo)
        {
            Debug.Log($"[Explosion] Stun de {duration}s aplicado a {target.name}");
        }
    }
    
    #endregion
    
    #region Audio
    
    /// <summary>
    /// Reproduce el sonido de la explosión en un GameObject separado
    /// </summary>
    private void PlayExplosionSound(Vector3 position)
    {
        if (explosionSound == null) return;
        
        // Crear GameObject temporal para el audio
        GameObject audioObject = new GameObject($"ExplosionSound_{gameObject.name}");
        audioObject.transform.position = position;
        
        // Añadir y configurar AudioSource
        AudioSource audioSource = audioObject.AddComponent<AudioSource>();
        audioSource.clip = explosionSound;
        audioSource.volume = soundVolume;
        audioSource.minDistance = soundMinDistance;
        audioSource.maxDistance = soundMaxDistance;
        audioSource.rolloffMode = soundRolloffMode;
        audioSource.priority = soundPriority;
        audioSource.spatialBlend = 1f; // 3D sound
        audioSource.playOnAwake = false;
        
        // Aplicar variación de pitch
        if (pitchVariation > 0)
        {
            float pitchOffset = Random.Range(-pitchVariation, pitchVariation);
            audioSource.pitch = 1f + pitchOffset;
        }
        
        // Reproducir el sonido
        audioSource.Play();
        
        // Destruir el GameObject cuando termine el audio
        float clipLength = explosionSound.length;
        Destroy(audioObject, clipLength + 0.1f); // Pequeño margen de seguridad
        
        if (showDebugInfo)
        {
            Debug.Log($"[Explosion] Sonido reproducido: {explosionSound.name}, duración: {clipLength:F2}s");
        }
    }
    
    #endregion
    
    #region Utilidades
    
    /// <summary>
    /// Verifica si hay línea de visión hacia el objetivo
    /// </summary>
    private bool HasLineOfSight(Vector3 from, Collider target)
    {
        Vector3 targetCenter = target.bounds.center;
        Vector3 direction = targetCenter - from;
        float distance = direction.magnitude;
        
        RaycastHit hit;
        if (Physics.Raycast(from, direction.normalized, out hit, distance, obstructionLayers))
        {
            // Verificar si el hit es el objetivo mismo
            return hit.collider == target;
        }
        
        return true;
    }
    
    /// <summary>
    /// Verifica si el objetivo es válido (enemigo vs aliado)
    /// </summary>
    private bool IsValidTarget(GameObject target)
    {
        // Implementar lógica de equipos/alianzas según tu juego
        // Por ahora, asumimos que todos son enemigos excepto el instigador
        
        if (explosionSource.instigator == null) return true;
        
        // No dañar al propio instigador
        if (target == explosionSource.instigator) return false;
        
        // Aquí añadirías lógica de equipos
        // Por ejemplo:
        // TeamComponent targetTeam = target.GetComponent<TeamComponent>();
        // TeamComponent instigatorTeam = explosionSource.instigator.GetComponent<TeamComponent>();
        // return targetTeam.teamId != instigatorTeam.teamId;
        
        return true;
    }
    
    /// <summary>
    /// Añade un objetivo a la lista de exclusión (ya dañado)
    /// </summary>
    public void AddExcludedTarget(GameObject target)
    {
        damagedTargets.Add(target);
    }
    
    /// <summary>
    /// Obtiene el radio actual de la explosión
    /// </summary>
    public float GetExplosionRadius() => explosionRadius;
    
    /// <summary>
    /// Establece el radio de la explosión (debe llamarse antes de Start)
    /// </summary>
    public void SetExplosionRadius(float radius)
    {
        explosionRadius = Mathf.Max(0.1f, radius);
    }
    
    #endregion
    
    #region Debug
    
    private void OnDrawGizmosSelected()
    {
        // Dibujar radio de explosión
        Gizmos.color = debugColor;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
        
        // Dibujar radio interior (donde el daño es máximo)
        Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, explosionRadius * 0.2f);
        
        // Si está en runtime, mostrar objetivos afectados
        if (Application.isPlaying && hasExploded)
        {
            Gizmos.color = Color.green;
            foreach (var target in damagedTargets)
            {
                if (target != null)
                {
                    Gizmos.DrawLine(transform.position, target.transform.position);
                    Gizmos.DrawWireCube(target.transform.position, Vector3.one * 0.5f);
                }
            }
        }
    }
    
    #endregion
}