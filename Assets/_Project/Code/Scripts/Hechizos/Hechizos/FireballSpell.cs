using UnityEngine;

/// <summary>
/// Ejemplo de hechizo de bola de fuego usando el nuevo sistema de comandos
/// Demuestra c�mo adaptar el comportamiento seg�n el tipo de comando usado
/// </summary>
[CreateAssetMenu(fileName = "Fireball Spell", menuName = "Spells/Fire/Fireball")]
public class FireballSpell : SpellBase
{
    [Header("Configuraci�n de Bola de Fuego")]
    [SerializeField] private float projectileSpeed = 15f;
    [SerializeField] private float damage = 25f;
    [SerializeField] private float explosionRadius = 3f;

    [Header("Configuraci�n Espec�fica por Comando")]
    [Tooltip("Velocidad adicional cuando se usa comando DIRECTIONAL (por intensidad del targeting)")]
    [SerializeField] private float directionalSpeedBonus = 5f;

    [Tooltip("Multiplicador de da�o cuando se usa gesto r�pido")]
    [SerializeField] private float gestureIntensityDamageMultiplier = 0.2f;

    /// <summary>
    /// Implementaci�n espec�fica del efecto de bola de fuego
    /// Ahora adapta el comportamiento seg�n el comando usado
    /// </summary>
    protected override void ExecuteSpellEffect(OriginData origin, SpellCastContext context)
    {
        Debug.Log($"Lanzando Fireball desde {OriginConfig.originType} con comando {context.commandUsed} en posici�n: {origin.position}");

        // Instanciar el proyectil en la posici�n calculada autom�ticamente
        if (spellPrefab != null)
        {
            GameObject fireball = Instantiate(spellPrefab, origin.position, origin.rotation);

            // Configurar el proyectil seg�n el comando usado
            ConfigureProjectile(fireball, origin, context);

            // Destruir despu�s de 10 segundos para evitar acumulaci�n
            Destroy(fireball, 10f);
        }
        else
        {
            // Fallback si no hay prefab - crear efecto b�sico
            Debug.Log($"�FIREBALL! Desde {origin.position} hacia {context.commandDirection} (Sin prefab configurado)");
        }
    }

    /// <summary>
    /// Configura el proyectil seg�n el contexto y comando usado
    /// </summary>
    private void ConfigureProjectile(GameObject fireball, OriginData origin, SpellCastContext context)
    {
        // Configurar f�sica del proyectil
        Rigidbody rb = fireball.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Vector3 launchDirection = GetLaunchDirection(origin, context);
            float finalSpeed = GetLaunchSpeed(context);

            rb.velocity = launchDirection * finalSpeed;

            Debug.Log($"Fireball lanzada: Direcci�n={launchDirection}, Velocidad={finalSpeed}");
        }

        // Configurar componente de da�o
        FireballProjectile projectileComponent = fireball.GetComponent<FireballProjectile>();
        if (projectileComponent == null)
        {
            projectileComponent = fireball.AddComponent<FireballProjectile>();
        }

        if (projectileComponent != null)
        {
            float finalDamage = GetFinalDamage(context);
            projectileComponent.Initialize(finalDamage, explosionRadius);

            Debug.Log($"Fireball configurada: Da�o={finalDamage}, Radio={explosionRadius}");
        }
    }

    /// <summary>
    /// Calcula la direcci�n de lanzamiento seg�n el origen y comando
    /// </summary>
    private Vector3 GetLaunchDirection(OriginData origin, SpellCastContext context)
    {
        switch (context.commandUsed)
        {
            case SpellCommandType.DIRECTIONAL:
                // El jugador apunt� espec�ficamente - usar esa direcci�n
                if (context.hasValidTarget)
                {
                    return (context.targetPosition - origin.position).normalized;
                }
                else
                {
                    return context.commandDirection;
                }

            case SpellCommandType.INSTANT:
                // Lanzamiento instant�neo - usar la configuraci�n de origen
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
                // Desde el suelo hacia arriba - usar direcci�n del gesto
                return context.commandDirection;

            case SpellCommandType.DESCEND:
                // Desde arriba hacia abajo - usar direcci�n del gesto
                return context.commandDirection;

            default:
                return origin.rotation * Vector3.forward;
        }
    }

    /// <summary>
    /// Calcula la velocidad final seg�n el comando y contexto
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
                    Debug.Log("Targeting preciso completado - velocidad m�xima");
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
    /// Calcula el da�o final seg�n el comando usado
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
                    Debug.Log("Targeting preciso - da�o aumentado");
                }
                else
                {
                    // Disparo por timeout - sin bonus (o peque�o penalty)
                    baseDamage *= 0.95f;
                    Debug.Log($"Disparo por timeout - da�o ligeramente reducido");
                }
                break;

            case SpellCommandType.EMERGE:
            case SpellCommandType.DESCEND:
                // Da�o basado en la intensidad del gesto
                float intensityBonus = context.commandIntensity * gestureIntensityDamageMultiplier;
                baseDamage += intensityBonus;
                break;

            case SpellCommandType.INSTANT:
                // Da�o base sin modificaciones
                break;
        }

        return baseDamage;
    }
}