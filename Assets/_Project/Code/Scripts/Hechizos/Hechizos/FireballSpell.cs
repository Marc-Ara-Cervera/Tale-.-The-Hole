using UnityEngine;

/// <summary>
/// Ejemplo de hechizo de bola de fuego usando el nuevo sistema de escalado
/// Demuestra c�mo integrar las propiedades escalables con el comportamiento del hechizo
/// </summary>
[CreateAssetMenu(fileName = "Fireball Spell", menuName = "Spells/Fire/Fireball")]
public class FireballSpell : SpellBase
{
    [Header("Configuraci�n de Bola de Fuego")]
    [SerializeField] private float baseProjectileSpeed = 15f;
    [SerializeField] private float baseExplosionRadius = 3f;

    [Header("Configuraci�n Espec�fica por Comando")]
    [Tooltip("Velocidad adicional cuando se usa comando DIRECTIONAL (por intensidad del targeting)")]
    [SerializeField] private float directionalSpeedBonus = 5f;

    [Tooltip("Multiplicador de da�o cuando se usa gesto r�pido")]
    [SerializeField] private float gestureIntensityDamageMultiplier = 0.2f;

    /// <summary>
    /// Implementaci�n espec�fica del efecto de bola de fuego con sistema de escalado
    /// NUEVO: Ahora recibe informaci�n de escalado para modificar las propiedades
    /// </summary>
    protected override void ExecuteSpellEffect(OriginData origin, SpellCastContext context)
    {
        // NUEVO: Obtener informaci�n de escalado del contexto (si existe)
        float currentScale = context.spellScale; // Nuevo campo que a�adiremos al context
        SpellScalingResult scalingResult = CalculateScaledProperties(currentScale);

        Debug.Log($"Lanzando Fireball escalada {currentScale:F1}x desde {OriginConfig.originType} con comando {context.commandUsed}");
        Debug.Log($"Propiedades escaladas - Da�o: {scalingResult.primaryPropertyValue:F1}, Velocidad: {scalingResult.speedMultiplier:F2}x");

        // Instanciar el proyectil en la posici�n calculada autom�ticamente
        if (spellPrefab != null)
        {
            GameObject fireball = Instantiate(spellPrefab, origin.position, origin.rotation);

            // NUEVO: Aplicar escalado visual al proyectil
            ApplyVisualScaling(fireball, currentScale);

            // Configurar el proyectil seg�n el comando usado Y la escala
            ConfigureProjectileWithScaling(fireball, origin, context, scalingResult);

            // Destruir despu�s de 10 segundos para evitar acumulaci�n
            Destroy(fireball, 10f);
        }
        else
        {
            // Fallback si no hay prefab - crear efecto b�sico
            Debug.Log($"�FIREBALL ESCALADA {currentScale:F1}x! Desde {origin.position} (Sin prefab configurado)");
        }
    }

    /// <summary>
    /// NUEVO: Aplica escalado visual al proyectil
    /// </summary>
    private void ApplyVisualScaling(GameObject fireball, float scale)
    {
        // Escalar el tama�o visual del proyectil
        fireball.transform.localScale = Vector3.one * scale;

        // Opcional: Ajustar intensidad de efectos de part�culas
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
    /// Configura el proyectil seg�n el contexto, comando usado Y escalado aplicado
    /// ACTUALIZADO: Ahora incorpora las propiedades escaladas
    /// </summary>
    private void ConfigureProjectileWithScaling(GameObject fireball, OriginData origin, SpellCastContext context, SpellScalingResult scalingResult)
    {
        // Configurar f�sica del proyectil
        Rigidbody rb = fireball.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Vector3 launchDirection = GetLaunchDirection(origin, context);
            float finalSpeed = GetLaunchSpeedWithScaling(context, scalingResult);

            rb.velocity = launchDirection * finalSpeed;

            Debug.Log($"Fireball lanzada: Direcci�n={launchDirection}, Velocidad={finalSpeed} (base: {baseProjectileSpeed}, escalado: {scalingResult.speedMultiplier:F2}x)");
        }

        // Configurar componente de da�o con escalado
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

            Debug.Log($"Fireball configurada: Da�o={finalDamage:F1} (escalado: {scalingResult.primaryPropertyValue:F1}), Radio={finalRadius:F1}");
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
                // Velocidad base sin modificaciones adicionales
                break;
        }

        return baseSpeed;
    }

    /// <summary>
    /// Calcula el da�o final incluyendo escalado
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
                // Da�o base sin modificaciones adicionales
                break;
        }

        return baseDamage;
    }

    /// <summary>
    /// NUEVO: Calcula el radio de explosi�n final basado en la escala
    /// </summary>
    private float GetFinalRadiusWithScaling(float scale)
    {
        // El radio escala directamente con la escala visual del hechizo
        return baseExplosionRadius * scale;
    }

    /// <summary>
    /// M�todo existente sin cambios (mantiene compatibilidad)
    /// </summary>
    private Vector3 GetLaunchDirection(OriginData origin, SpellCastContext context)
    {
        switch (OriginConfig.originType)
        {
            case SpellOriginType.STAFF_TIP:
                // CORREGIDO: Dirigir hacia el punto objetivo si hay targeting v�lido
                if (context.hasValidTarget && context.commandUsed == SpellCommandType.DIRECTIONAL)
                {
                    // Calcular direcci�n desde el origen hacia el punto objetivo
                    Vector3 directionToTarget = (context.targetPosition - origin.position).normalized;
                    return directionToTarget;
                }
                else
                {
                    // Fallback: usar la direcci�n del bast�n si no hay targeting v�lido
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
                // Explotar en el lugar (velocidad cero o m�nima)
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