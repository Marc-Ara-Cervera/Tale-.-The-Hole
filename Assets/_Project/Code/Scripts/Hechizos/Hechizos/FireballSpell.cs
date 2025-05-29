using UnityEngine;

/// <summary>
/// Ejemplo de hechizo de bola de fuego usando el nuevo sistema de comandos
/// Demuestra cómo adaptar el comportamiento según el tipo de comando usado
/// </summary>
[CreateAssetMenu(fileName = "Fireball Spell", menuName = "Spells/Fire/Fireball")]
public class FireballSpell : SpellBase
{
    [Header("Configuración de Bola de Fuego")]
    [SerializeField] private float projectileSpeed = 15f;
    [SerializeField] private float damage = 25f;
    [SerializeField] private float explosionRadius = 3f;

    [Header("Configuración Específica por Comando")]
    [Tooltip("Velocidad adicional cuando se usa comando DIRECTIONAL (por intensidad del targeting)")]
    [SerializeField] private float directionalSpeedBonus = 5f;

    [Tooltip("Multiplicador de daño cuando se usa gesto rápido")]
    [SerializeField] private float gestureIntensityDamageMultiplier = 0.2f;

    /// <summary>
    /// Implementación específica del efecto de bola de fuego
    /// Ahora adapta el comportamiento según el comando usado
    /// </summary>
    protected override void ExecuteSpellEffect(OriginData origin, SpellCastContext context)
    {
        Debug.Log($"Lanzando Fireball desde {OriginConfig.originType} con comando {context.commandUsed} en posición: {origin.position}");

        // Instanciar el proyectil en la posición calculada automáticamente
        if (spellPrefab != null)
        {
            GameObject fireball = Instantiate(spellPrefab, origin.position, origin.rotation);

            // Configurar el proyectil según el comando usado
            ConfigureProjectile(fireball, origin, context);

            // Destruir después de 10 segundos para evitar acumulación
            Destroy(fireball, 10f);
        }
        else
        {
            // Fallback si no hay prefab - crear efecto básico
            Debug.Log($"¡FIREBALL! Desde {origin.position} hacia {context.commandDirection} (Sin prefab configurado)");
        }
    }

    /// <summary>
    /// Configura el proyectil según el contexto y comando usado
    /// </summary>
    private void ConfigureProjectile(GameObject fireball, OriginData origin, SpellCastContext context)
    {
        // Configurar física del proyectil
        Rigidbody rb = fireball.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Vector3 launchDirection = GetLaunchDirection(origin, context);
            float finalSpeed = GetLaunchSpeed(context);

            rb.velocity = launchDirection * finalSpeed;

            Debug.Log($"Fireball lanzada: Dirección={launchDirection}, Velocidad={finalSpeed}");
        }

        // Configurar componente de daño
        FireballProjectile projectileComponent = fireball.GetComponent<FireballProjectile>();
        if (projectileComponent == null)
        {
            projectileComponent = fireball.AddComponent<FireballProjectile>();
        }

        if (projectileComponent != null)
        {
            float finalDamage = GetFinalDamage(context);
            projectileComponent.Initialize(finalDamage, explosionRadius);

            Debug.Log($"Fireball configurada: Daño={finalDamage}, Radio={explosionRadius}");
        }
    }

    /// <summary>
    /// Calcula la dirección de lanzamiento según el origen y comando
    /// </summary>
    private Vector3 GetLaunchDirection(OriginData origin, SpellCastContext context)
    {
        switch (context.commandUsed)
        {
            case SpellCommandType.DIRECTIONAL:
                // El jugador apuntó específicamente - usar esa dirección
                if (context.hasValidTarget)
                {
                    return (context.targetPosition - origin.position).normalized;
                }
                else
                {
                    return context.commandDirection;
                }

            case SpellCommandType.INSTANT:
                // Lanzamiento instantáneo - usar la configuración de origen
                switch (OriginConfig.originType)
                {
                    case SpellOriginType.STAFF_TIP:
                        return origin.rotation * Vector3.forward;

                    case SpellOriginType.PLAYER_CENTER:
                    case SpellOriginType.PLAYER_FRONT:
                        if (context.hasValidTarget)
                        {
                            return (context.targetPosition - origin.position).normalized;
                        }
                        else
                        {
                            return context.playerTransform.forward;
                        }

                    case SpellOriginType.TARGET_ABOVE:
                        return Vector3.down;

                    case SpellOriginType.TARGET_POINT:
                    case SpellOriginType.TARGET_SURFACE:
                        // Para estas posiciones, explotar in-situ
                        return Vector3.up * 0.1f;

                    default:
                        return origin.rotation * Vector3.forward;
                }

            case SpellCommandType.EMERGE:
                // Desde el suelo hacia arriba - usar dirección del gesto
                return context.commandDirection;

            case SpellCommandType.DESCEND:
                // Desde arriba hacia abajo - usar dirección del gesto
                return context.commandDirection;

            default:
                return origin.rotation * Vector3.forward;
        }
    }

    /// <summary>
    /// Calcula la velocidad final según el comando y contexto
    /// </summary>
    private float GetLaunchSpeed(SpellCastContext context)
    {
        float baseSpeed = projectileSpeed;

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
                // Velocidad base sin modificaciones
                break;
        }

        return baseSpeed;
    }

    /// <summary>
    /// Calcula el daño final según el comando usado
    /// </summary>
    private float GetFinalDamage(SpellCastContext context)
    {
        float baseDamage = damage;

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
                // Daño base sin modificaciones
                break;
        }

        return baseDamage;
    }
}