using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Tipos de da�o elemental para las explosiones
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
/// Informaci�n sobre el origen de la explosi�n
/// </summary>
[System.Serializable]
public struct ExplosionSource
{
    public GameObject instigator;       // Quien caus� la explosi�n
    public float baseDamage;           // Da�o base de la habilidad
    public DamageType damageType;      // Tipo de da�o
    public HashSet<GameObject> excludeTargets; // Objetivos a excluir (ej: ya da�ados por proyectil)
    
    public ExplosionSource(GameObject source, float damage, DamageType type = DamageType.Physical)
    {
        instigator = source;
        baseDamage = damage;
        damageType = type;
        excludeTargets = new HashSet<GameObject>();
    }
}

/// <summary>
/// Sistema de explosiones que aplica da�o en �rea con falloff por distancia
/// </summary>
public class Explosion : MonoBehaviour
{
    [Header("---- CONFIGURACI�N DE EXPLOSI�N ----")]
    [SerializeField, Tooltip("Radio de la explosi�n en metros")]
    private float explosionRadius = 5f;
    
    [SerializeField, Tooltip("Da�o por defecto si no viene de una habilidad")]
    private float defaultDamage = 50f;
    
    [SerializeField, Tooltip("Tipo de da�o por defecto")]
    private DamageType defaultDamageType = DamageType.Physical;
    
    [Header("---- CONFIGURACI�N DE FALLOFF ----")]
    [SerializeField, Tooltip("Curva de da�o por distancia (0 = centro, 1 = borde)")]
    private AnimationCurve damageFalloffCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0.2f);
    
    [SerializeField, Tooltip("�El da�o m�nimo en el borde es 0?")]
    private bool zeroAtEdge = false;
    
    [SerializeField, Tooltip("Da�o m�nimo garantizado (% del da�o base)"), Range(0f, 1f)]
    private float minimumDamagePercent = 0.1f;
    
    [Header("---- F�SICA Y FUERZAS ----")]
    [SerializeField, Tooltip("Fuerza de empuje")]
    private float knockbackForce = 10f;
    
    [SerializeField, Tooltip("Curva de fuerza por distancia")]
    private AnimationCurve knockbackFalloffCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0.3f);
    
    [SerializeField, Tooltip("�Levantar objetivos del suelo?")]
    private bool liftTargets = true;
    
    [SerializeField, Tooltip("Multiplicador de fuerza vertical"), Range(0f, 2f)]
    private float verticalForceMultiplier = 0.5f;
    
    [Header("---- TARGETING ----")]
    [SerializeField, Tooltip("Capas que puede afectar la explosi�n")]
    private LayerMask affectedLayers = -1;
    
    [SerializeField, Tooltip("�Puede da�ar aliados?")]
    private bool canDamageAllies = false;
    
    [SerializeField, Tooltip("�Requiere l�nea de visi�n?")]
    private bool requireLineOfSight = true;
    
    [SerializeField, Tooltip("Capas que bloquean la explosi�n")]
    private LayerMask obstructionLayers = -1;
    
    [Header("---- EFECTOS ADICIONALES ----")]
    [SerializeField, Tooltip("Duraci�n del aturdimiento"), Range(0f, 5f)]
    private float stunDuration = 0f;
    
    [SerializeField, Tooltip("Probabilidad de efecto cr�tico"), Range(0f, 1f)]
    private float criticalChance = 0.1f;
    
    [SerializeField, Tooltip("Multiplicador de da�o cr�tico")]
    private float criticalMultiplier = 2f;
    
    [Header("---- AUDIO ----")]
    [SerializeField, Tooltip("Sonido de la explosi�n")]
    private AudioClip explosionSound;
    
    [SerializeField, Tooltip("Volumen del sonido"), Range(0f, 1f)]
    private float soundVolume = 1f;
    
    [SerializeField, Tooltip("Distancia m�nima del sonido")]
    private float soundMinDistance = 1f;
    
    [SerializeField, Tooltip("Distancia m�xima del sonido")]
    private float soundMaxDistance = 50f;
    
    [SerializeField, Tooltip("Tipo de rolloff del sonido")]
    private AudioRolloffMode soundRolloffMode = AudioRolloffMode.Logarithmic;
    
    [SerializeField, Tooltip("Prioridad del sonido (0 = m�s alta)"), Range(0, 256)]
    private int soundPriority = 128;
    
    [SerializeField, Tooltip("Variaci�n aleatoria del pitch"), Range(0f, 0.5f)]
    private float pitchVariation = 0.1f;
    
    [Header("---- DEBUG ----")]
    [SerializeField] private bool showDebugInfo = true;
    [SerializeField] private Color debugColor = new Color(1f, 0.5f, 0f, 0.3f);
    
    // Estado interno
    private ExplosionSource explosionSource;
    private bool hasExploded = false;
    private HashSet<GameObject> damagedTargets = new HashSet<GameObject>();
    
    #region Inicializaci�n
    
    /// <summary>
    /// Inicializa la explosi�n con informaci�n de la fuente
    /// </summary>
    public void Initialize(ExplosionSource source)
    {
        explosionSource = source;
        
        // Si hay objetivos a excluir, a�adirlos a la lista
        if (source.excludeTargets != null)
        {
            foreach (var target in source.excludeTargets)
            {
                damagedTargets.Add(target);
            }
        }
    }
    
    /// <summary>
    /// Inicializaci�n simple con solo da�o
    /// </summary>
    public void Initialize(float damage, GameObject instigator = null)
    {
        explosionSource = new ExplosionSource(instigator, damage, defaultDamageType);
    }
    
    private void Start()
    {
        // Explotar inmediatamente
        Explode();
        
        // Auto-destruir despu�s de un tiempo para limpiar
        Destroy(gameObject, 0.5f);
    }
    
    #endregion
    
    #region L�gica de Explosi�n
    
    /// <summary>
    /// Ejecuta la explosi�n aplicando efectos a todos los objetivos en rango
    /// </summary>
    private void Explode()
    {
        if (hasExploded) return;
        hasExploded = true;
        
        Vector3 explosionCenter = transform.position;
        
        // Reproducir sonido de explosi�n
        PlayExplosionSound(explosionCenter);
        
        // Obtener el da�o base
        float baseDamage = explosionSource.baseDamage > 0 ? explosionSource.baseDamage : defaultDamage;
        
        if (showDebugInfo)
        {
            Debug.Log($"[Explosion] Explotando en {explosionCenter} con {baseDamage} de da�o base, radio {explosionRadius}m");
        }
        
        // Encontrar todos los objetivos potenciales
        Collider[] potentialTargets = Physics.OverlapSphere(explosionCenter, explosionRadius, affectedLayers);
        
        // Procesar cada objetivo
        foreach (Collider col in potentialTargets)
        {
            ProcessTarget(col, explosionCenter, baseDamage);
        }
        
        // Notificar estad�sticas finales
        if (showDebugInfo)
        {
            Debug.Log($"[Explosion] Procesados {potentialTargets.Length} objetivos, {damagedTargets.Count} da�ados");
        }
    }
    
    /// <summary>
    /// Procesa un objetivo individual
    /// </summary>
    private void ProcessTarget(Collider target, Vector3 explosionCenter, float baseDamage)
    {
        GameObject targetObject = target.gameObject;
        
        // Verificar si ya fue da�ado
        if (damagedTargets.Contains(targetObject))
        {
            if (showDebugInfo)
                Debug.Log($"[Explosion] {targetObject.name} ya fue da�ado, ignorando");
            return;
        }
        
        // Verificar l�nea de visi�n si es necesario
        if (requireLineOfSight && !HasLineOfSight(explosionCenter, target))
        {
            if (showDebugInfo)
                Debug.Log($"[Explosion] {targetObject.name} bloqueado por obst�culo");
            return;
        }
        
        // Verificar si es un objetivo v�lido
        var damageable = target.GetComponent<iDa�able>();
        if (damageable == null)
        {
            // Intentar aplicar f�sica aunque no sea da�able
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
        
        // Calcular distancia y da�o
        float distance = Vector3.Distance(explosionCenter, target.transform.position);
        float normalizedDistance = Mathf.Clamp01(distance / explosionRadius);
        
        // Calcular da�o con falloff
        float damageMultiplier = damageFalloffCurve.Evaluate(normalizedDistance);
        
        // Aplicar da�o m�nimo si no es zero at edge
        if (!zeroAtEdge)
        {
            damageMultiplier = Mathf.Max(damageMultiplier, minimumDamagePercent);
        }
        
        float finalDamage = baseDamage * damageMultiplier;
        
        // Verificar cr�tico
        if (Random.value < criticalChance)
        {
            finalDamage *= criticalMultiplier;
            if (showDebugInfo)
                Debug.Log($"[Explosion] �Golpe cr�tico en {targetObject.name}!");
        }
        
        // Aplicar el da�o
        damageable.TakeDamage(finalDamage, explosionSource.instigator);
        damagedTargets.Add(targetObject);
        
        // Aplicar efectos adicionales
        ApplyAdditionalEffects(target, explosionCenter, normalizedDistance);
        
        if (showDebugInfo)
        {
            Debug.Log($"[Explosion] {targetObject.name} recibi� {finalDamage:F1} da�o (distancia: {distance:F1}m, multiplicador: {damageMultiplier:F2})");
        }
    }
    
    #endregion
    
    #region Efectos Adicionales
    
    /// <summary>
    /// Aplica efectos adicionales como knockback y stun
    /// </summary>
    private void ApplyAdditionalEffects(Collider target, Vector3 explosionCenter, float normalizedDistance)
    {
        // Aplicar fuerza f�sica
        ApplyPhysicsForce(target, explosionCenter);
        
        // Aplicar stun si est� configurado
        if (stunDuration > 0)
        {
            ApplyStun(target.gameObject, stunDuration);
        }
        
        // Aqu� puedes a�adir m�s efectos como:
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
        
        // Calcular direcci�n y distancia
        Vector3 direction = (target.transform.position - explosionCenter).normalized;
        float distance = Vector3.Distance(explosionCenter, target.transform.position);
        float normalizedDistance = Mathf.Clamp01(distance / explosionRadius);
        
        // Calcular fuerza con falloff
        float forceMultiplier = knockbackFalloffCurve.Evaluate(normalizedDistance);
        float finalForce = knockbackForce * forceMultiplier;
        
        // A�adir componente vertical si est� habilitado
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
        // Aqu� implementar�as tu sistema de stun
        // Por ejemplo, buscar un componente que maneje estados
        
        if (showDebugInfo)
        {
            Debug.Log($"[Explosion] Stun de {duration}s aplicado a {target.name}");
        }
    }
    
    #endregion
    
    #region Audio
    
    /// <summary>
    /// Reproduce el sonido de la explosi�n en un GameObject separado
    /// </summary>
    private void PlayExplosionSound(Vector3 position)
    {
        if (explosionSound == null) return;
        
        // Crear GameObject temporal para el audio
        GameObject audioObject = new GameObject($"ExplosionSound_{gameObject.name}");
        audioObject.transform.position = position;
        
        // A�adir y configurar AudioSource
        AudioSource audioSource = audioObject.AddComponent<AudioSource>();
        audioSource.clip = explosionSound;
        audioSource.volume = soundVolume;
        audioSource.minDistance = soundMinDistance;
        audioSource.maxDistance = soundMaxDistance;
        audioSource.rolloffMode = soundRolloffMode;
        audioSource.priority = soundPriority;
        audioSource.spatialBlend = 1f; // 3D sound
        audioSource.playOnAwake = false;
        
        // Aplicar variaci�n de pitch
        if (pitchVariation > 0)
        {
            float pitchOffset = Random.Range(-pitchVariation, pitchVariation);
            audioSource.pitch = 1f + pitchOffset;
        }
        
        // Reproducir el sonido
        audioSource.Play();
        
        // Destruir el GameObject cuando termine el audio
        float clipLength = explosionSound.length;
        Destroy(audioObject, clipLength + 0.1f); // Peque�o margen de seguridad
        
        if (showDebugInfo)
        {
            Debug.Log($"[Explosion] Sonido reproducido: {explosionSound.name}, duraci�n: {clipLength:F2}s");
        }
    }
    
    #endregion
    
    #region Utilidades
    
    /// <summary>
    /// Verifica si hay l�nea de visi�n hacia el objetivo
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
    /// Verifica si el objetivo es v�lido (enemigo vs aliado)
    /// </summary>
    private bool IsValidTarget(GameObject target)
    {
        // Implementar l�gica de equipos/alianzas seg�n tu juego
        // Por ahora, asumimos que todos son enemigos excepto el instigador
        
        if (explosionSource.instigator == null) return true;
        
        // No da�ar al propio instigador
        if (target == explosionSource.instigator) return false;
        
        // Aqu� a�adir�as l�gica de equipos
        // Por ejemplo:
        // TeamComponent targetTeam = target.GetComponent<TeamComponent>();
        // TeamComponent instigatorTeam = explosionSource.instigator.GetComponent<TeamComponent>();
        // return targetTeam.teamId != instigatorTeam.teamId;
        
        return true;
    }
    
    /// <summary>
    /// A�ade un objetivo a la lista de exclusi�n (ya da�ado)
    /// </summary>
    public void AddExcludedTarget(GameObject target)
    {
        damagedTargets.Add(target);
    }
    
    /// <summary>
    /// Obtiene el radio actual de la explosi�n
    /// </summary>
    public float GetExplosionRadius() => explosionRadius;
    
    /// <summary>
    /// Establece el radio de la explosi�n (debe llamarse antes de Start)
    /// </summary>
    public void SetExplosionRadius(float radius)
    {
        explosionRadius = Mathf.Max(0.1f, radius);
    }
    
    #endregion
    
    #region Debug
    
    private void OnDrawGizmosSelected()
    {
        // Dibujar radio de explosi�n
        Gizmos.color = debugColor;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
        
        // Dibujar radio interior (donde el da�o es m�ximo)
        Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, explosionRadius * 0.2f);
        
        // Si est� en runtime, mostrar objetivos afectados
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