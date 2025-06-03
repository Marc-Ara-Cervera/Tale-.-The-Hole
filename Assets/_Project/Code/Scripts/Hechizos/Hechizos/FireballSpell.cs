using UnityEngine;

/// <summary>
/// Ejemplo de hechizo de bola de fuego usando el nuevo sistema de escalado
/// Demuestra cómo integrar las propiedades escalables con el comportamiento del hechizo
/// </summary>
[CreateAssetMenu(fileName = "Fireball Spell", menuName = "Spells/Fire/Fireball")]
public class FireballSpell : SpellBase
{
    [Header("Configuración de Bola de Fuego")]
    [SerializeField] private float baseProjectileSpeed = 15f;
    [SerializeField] private float baseExplosionRadius = 3f;

    [Header("Configuración Específica por Comando")]
    [Tooltip("Velocidad adicional cuando se usa comando DIRECTIONAL (por intensidad del targeting)")]
    [SerializeField] private float directionalSpeedBonus = 5f;

    [Tooltip("Multiplicador de daño cuando se usa gesto rápido")]
    [SerializeField] private float gestureIntensityDamageMultiplier = 0.2f;

    /// <summary>
    /// Implementación específica del efecto de bola de fuego con sistema de escalado
    /// NUEVO: Ahora recibe información de escalado para modificar las propiedades
    /// </summary>
    protected override void ExecuteSpellEffect(OriginData origin, SpellCastContext context)
    {
        // NUEVO: Obtener información de escalado del contexto (si existe)
        float currentScale = context.spellScale; // Nuevo campo que añadiremos al context
        SpellScalingResult scalingResult = CalculateScaledProperties(currentScale);

        Debug.Log($"Lanzando Fireball escalada {currentScale:F1}x desde {OriginConfig.originType} con comando {context.commandUsed}");
        Debug.Log($"Propiedades escaladas - Daño: {scalingResult.primaryPropertyValue:F1}, Velocidad: {scalingResult.speedMultiplier:F2}x");

        // Instanciar el proyectil en la posición calculada automáticamente
        if (spellPrefab != null)
        {
            GameObject fireball = Instantiate(spellPrefab, origin.position, origin.rotation);

            // NUEVO: Aplicar escalado visual al proyectil
            ApplyVisualScaling(fireball, currentScale);

            // Configurar el proyectil según el comando usado Y la escala
            ConfigureProjectileWithScaling(fireball, origin, context, scalingResult);

            // Destruir después de 10 segundos para evitar acumulación
            Destroy(fireball, 10f);
        }
        else
        {
            // Fallback si no hay prefab - crear efecto básico
            Debug.Log($"¡FIREBALL ESCALADA {currentScale:F1}x! Desde {origin.position} (Sin prefab configurado)");
        }
    }

    /// <summary>
    /// NUEVO: Aplica escalado visual al proyectil
    /// </summary>
    private void ApplyVisualScaling(GameObject fireball, float scale)
    {
        // Escalar el tamaño visual del proyectil
        fireball.transform.localScale = Vector3.one * scale;

        // Opcional: Ajustar intensidad de efectos de partículas
        ParticleSystem[] particles = fireball.GetComponentsInChildren<ParticleSystem>();
        foreach (ParticleSystem ps in particles)
        {
            var main = ps.main;
            main.startSize = main.startSize.constant * scale;

            var shape = ps.shape;
            shape.radius = shape.radius * scale;
        }
    }

    /// <summary>
    /// Configura el proyectil según el contexto, comando usado Y escalado aplicado
    /// ACTUALIZADO: Ahora incorpora las propiedades escaladas
    /// </summary>
    private void ConfigureProjectileWithScaling(GameObject fireball, OriginData origin, SpellCastContext context, SpellScalingResult scalingResult)
    {
        // Configurar física del proyectil
        Rigidbody rb = fireball.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Vector3 launchDirection = GetLaunchDirection(origin, context);
            float finalSpeed = GetLaunchSpeedWithScaling(context, scalingResult);

            rb.velocity = launchDirection * finalSpeed;

            Debug.Log($"Fireball lanzada: Dirección={launchDirection}, Velocidad={finalSpeed} (base: {baseProjectileSpeed}, escalado: {scalingResult.speedMultiplier:F2}x)");
        }

        // Configurar componente de daño con escalado
        FireballProjectile projectileComponent = fireball.GetComponent<FireballProjectile>();
        if (projectileComponent == null)
        {
            projectileComponent = fireball.AddComponent<FireballProjectile>();
        }

        if (projectileComponent != null)
        {
            float finalDamage = GetFinalDamageWithScaling(context, scalingResult);
            float finalRadius = GetFinalRadiusWithScaling(context.spellScale);

            projectileComponent.Initialize(finalDamage, finalRadius);

            Debug.Log($"Fireball configurada: Daño={finalDamage:F1} (escalado: {scalingResult.primaryPropertyValue:F1}), Radio={finalRadius:F1}");
        }
    }

    /// <summary>
    /// Calcula la velocidad final incluyendo escalado
    /// ACTUALIZADO: Incorpora el multiplicador de velocidad escalado
    /// </summary>
    private float GetLaunchSpeedWithScaling(SpellCastContext context, SpellScalingResult scalingResult)
    {
        float baseSpeed = baseProjectileSpeed * scalingResult.speedMultiplier; // NUEVO: Aplicar escalado de velocidad

        switch (context.commandUsed)
        {
            case SpellCommandType.DIRECTIONAL:
                // Distinguir entre targeting preciso vs timeout
                if (context.commandIntensity >= 1f)
                {
                    // Targeting preciso completado - bonus completo
                    baseSpeed += directionalSpeedBonus;
                    Debug.Log("Targeting preciso completado - velocidad máxima");
                }
                else
                {
                    // Disparo por timeout - bonus reducido
                    baseSpeed += directionalSpeedBonus * context.commandIntensity;
                    Debug.Log($"Disparo por timeout - velocidad reducida (intensidad: {context.commandIntensity})");
                }
                break;

            case SpellCommandType.EMERGE:
            case SpellCommandType.DESCEND:
                // Velocidad basada en la intensidad del gesto
                float intensityMultiplier = Mathf.Clamp(context.commandIntensity / 3f, 0.5f, 2f);
                baseSpeed *= intensityMultiplier;
                break;

            case SpellCommandType.INSTANT:
                // Velocidad base sin modificaciones adicionales
                break;
        }

        return baseSpeed;
    }

    /// <summary>
    /// Calcula el daño final incluyendo escalado
    /// ACTUALIZADO: Usa la propiedad principal escalada como base
    /// </summary>
    private float GetFinalDamageWithScaling(SpellCastContext context, SpellScalingResult scalingResult)
    {
        float baseDamage = scalingResult.primaryPropertyValue; // NUEVO: Usar valor escalado como base

        switch (context.commandUsed)
        {
            case SpellCommandType.DIRECTIONAL:
                // Distinguir entre targeting preciso vs timeout
                if (context.commandIntensity >= 1f)
                {
                    // Targeting preciso completado - bonus completo
                    baseDamage *= 1.1f;
                    Debug.Log("Targeting preciso - daño aumentado");
                }
                else
                {
                    // Disparo por timeout - sin bonus (o pequeño penalty)
                    baseDamage *= 0.95f;
                    Debug.Log($"Disparo por timeout - daño ligeramente reducido");
                }
                break;

            case SpellCommandType.EMERGE:
            case SpellCommandType.DESCEND:
                // Daño basado en la intensidad del gesto
                float intensityBonus = context.commandIntensity * gestureIntensityDamageMultiplier;
                baseDamage += intensityBonus;
                break;

            case SpellCommandType.INSTANT:
                // Daño base sin modificaciones adicionales
                break;
        }

        return baseDamage;
    }

    /// <summary>
    /// NUEVO: Calcula el radio de explosión final basado en la escala
    /// </summary>
    private float GetFinalRadiusWithScaling(float scale)
    {
        // El radio escala directamente con la escala visual del hechizo
        return baseExplosionRadius * scale;
    }

    /// <summary>
    /// Método existente sin cambios (mantiene compatibilidad)
    /// </summary>
    private Vector3 GetLaunchDirection(OriginData origin, SpellCastContext context)
    {
        switch (OriginConfig.originType)
        {
            case SpellOriginType.STAFF_TIP:
                // CORREGIDO: Dirigir hacia el punto objetivo si hay targeting válido
                if (context.hasValidTarget && context.commandUsed == SpellCommandType.DIRECTIONAL)
                {
                    // Calcular dirección desde el origen hacia el punto objetivo
                    Vector3 directionToTarget = (context.targetPosition - origin.position).normalized;
                    return directionToTarget;
                }
                else
                {
                    // Fallback: usar la dirección del bastón si no hay targeting válido
                    return origin.rotation * Vector3.forward;
                }

            case SpellOriginType.PLAYER_CENTER:
            case SpellOriginType.PLAYER_FRONT:
                // Desde el jugador hacia el objetivo
                if (context.hasValidTarget)
                {
                    return (context.targetPosition - origin.position).normalized;
                }
                else
                {
                    return context.playerTransform.forward;
                }

            case SpellOriginType.TARGET_ABOVE:
                // Desde arriba hacia abajo
                return Vector3.down;

            case SpellOriginType.TARGET_POINT:
            case SpellOriginType.TARGET_SURFACE:
                // Explotar en el lugar (velocidad cero o mínima)
                return Vector3.up * 0.1f;

            default:
                // Para otros tipos, dirigir hacia el objetivo si existe
                if (context.hasValidTarget)
                {
                    return (context.targetPosition - origin.position).normalized;
                }
                return origin.rotation * Vector3.forward;
        }
    }
}