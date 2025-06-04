using UnityEngine;

/// <summary>
/// Proyectil simple para la bola de fuego que usa el nuevo sistema de explosiones
/// </summary>
public class FireballProjectileSimple : MonoBehaviour
{
    private float damage;
    private float explosionRadius;
    private GameObject explosionPrefab;
    private GameObject instigator;
    private bool hasExploded = false;

    public void Initialize(float projectileDamage, float radius, GameObject explosion, GameObject caster)
    {
        damage = projectileDamage;
        explosionRadius = radius;
        explosionPrefab = explosion;
        instigator = caster;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasExploded) return;
        if (other.CompareTag("Player")) return;

        Explode(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (hasExploded) return;
        if (collision.gameObject.CompareTag("Player")) return;

        Explode(collision.gameObject);
    }

    private void Explode(GameObject hitTarget)
    {
        hasExploded = true;

        // Crear la explosi�n
        if (explosionPrefab != null)
        {
            GameObject explosionObj = Instantiate(explosionPrefab, transform.position, Quaternion.identity);

            // Configurar la explosi�n
            Explosion explosion = explosionObj.GetComponent<Explosion>();
            if (explosion != null)
            {
                // Crear fuente de explosi�n con el objetivo impactado excluido
                ExplosionSource source = new ExplosionSource(instigator, damage, DamageType.Fire);

                // Si impactamos directamente a un enemigo, aplicar da�o directo primero
                iDa�able directTarget = hitTarget.GetComponent<iDa�able>();
                if (directTarget != null)
                {
                    // Aplicar da�o directo al impacto (100% del da�o)
                    directTarget.TakeDamage(damage, instigator);

                    // Excluir este objetivo de la explosi�n para evitar doble da�o
                    source.excludeTargets.Add(hitTarget);
                }

                // Configurar e inicializar la explosi�n
                explosion.SetExplosionRadius(explosionRadius);
                explosion.Initialize(source);
            }
        }

        // Destruir el proyectil
        Destroy(gameObject);
    }
}

/// <summary>
/// Hechizo de bola de fuego actualizado con el nuevo sistema de explosiones
/// </summary>
[CreateAssetMenu(fileName = "Fireball Spell", menuName = "Spells/Fire/Fireball")]
public class FireballSpell : SpellBase
{
    [Header("Configuraci�n de Bola de Fuego")]
    [SerializeField] private float baseProjectileSpeed = 15f;
    [SerializeField] private float baseExplosionRadius = 3f;

    [Header("Prefabs de Explosi�n")]
    [SerializeField, Tooltip("Prefab de la explosi�n que se crear� al impactar")]
    private GameObject explosionPrefab;

    [Header("Configuraci�n Espec�fica por Comando")]
    [Tooltip("Velocidad adicional cuando se usa comando DIRECTIONAL")]
    [SerializeField] private float directionalSpeedBonus = 5f;

    [Tooltip("Multiplicador de da�o cuando se usa gesto r�pido")]
    [SerializeField] private float gestureIntensityDamageMultiplier = 0.2f;

    [Tooltip("Bonus de radio de explosi�n por escala")]
    [SerializeField] private AnimationCurve explosionRadiusCurve = AnimationCurve.Linear(0.5f, 0.5f, 4f, 4f);

    protected override void ExecuteSpellEffect(OriginData origin, SpellCastContext context)
    {
        float currentScale = context.spellScale;
        SpellScalingResult scalingResult = CalculateScaledProperties(currentScale);

        Debug.Log($"Lanzando Fireball escalada {currentScale:F1}x desde {OriginConfig.originType} con comando {context.commandUsed}");

        if (spellPrefab != null)
        {
            GameObject fireball = Instantiate(spellPrefab, origin.position, origin.rotation);

            // Aplicar escalado visual
            ApplyVisualScaling(fireball, currentScale);

            // Configurar el proyectil
            ConfigureProjectileWithScaling(fireball, origin, context, scalingResult);

            // Auto-destruir despu�s de 10 segundos
            Destroy(fireball, 10f);
        }
        else
        {
            Debug.LogError("[FireballSpell] No hay prefab de proyectil configurado");
        }
    }

    private void ApplyVisualScaling(GameObject fireball, float scale)
    {
        fireball.transform.localScale = Vector3.one * scale;

        // Escalar efectos de part�culas
        ParticleSystem[] particles = fireball.GetComponentsInChildren<ParticleSystem>();
        foreach (ParticleSystem ps in particles)
        {
            var main = ps.main;
            main.startSize = main.startSize.constant * scale;

            var shape = ps.shape;
            shape.radius = shape.radius * scale;
        }
    }

    private void ConfigureProjectileWithScaling(GameObject fireball, OriginData origin, SpellCastContext context, SpellScalingResult scalingResult)
    {
        // Configurar f�sica
        Rigidbody rb = fireball.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = fireball.AddComponent<Rigidbody>();
            rb.useGravity = false;
        }

        // Configurar collider si no existe
        Collider col = fireball.GetComponent<Collider>();
        if (col == null)
        {
            SphereCollider sphereCol = fireball.AddComponent<SphereCollider>();
            sphereCol.isTrigger = true;
            sphereCol.radius = 0.2f * context.spellScale;
        }

        // Aplicar velocidad
        Vector3 launchDirection = GetLaunchDirection(origin, context);
        float finalSpeed = GetLaunchSpeedWithScaling(context, scalingResult);
        rb.velocity = launchDirection * finalSpeed;

        // Configurar componente de proyectil
        FireballProjectileSimple projectile = fireball.GetComponent<FireballProjectileSimple>();
        if (projectile == null)
        {
            projectile = fireball.AddComponent<FireballProjectileSimple>();
        }

        // Calcular valores finales
        float finalDamage = GetFinalDamageWithScaling(context, scalingResult);
        float finalRadius = GetFinalRadiusWithScaling(context.spellScale);

        // Inicializar el proyectil
        GameObject caster = context.caster != null ? context.caster.gameObject : null;
        projectile.Initialize(finalDamage, finalRadius, explosionPrefab, caster);

        Debug.Log($"Fireball configurada: Da�o={finalDamage:F1}, Radio={finalRadius:F1}m, Velocidad={finalSpeed:F1}m/s");
    }

    private float GetLaunchSpeedWithScaling(SpellCastContext context, SpellScalingResult scalingResult)
    {
        float baseSpeed = baseProjectileSpeed * scalingResult.speedMultiplier;

        switch (context.commandUsed)
        {
            case SpellCommandType.DIRECTIONAL:
                if (context.commandIntensity >= 1f)
                {
                    baseSpeed += directionalSpeedBonus;
                }
                else
                {
                    baseSpeed += directionalSpeedBonus * context.commandIntensity;
                }
                break;

            case SpellCommandType.EMERGE:
            case SpellCommandType.DESCEND:
                float intensityMultiplier = Mathf.Clamp(context.commandIntensity / 3f, 0.5f, 2f);
                baseSpeed *= intensityMultiplier;
                break;
        }

        return baseSpeed;
    }

    private float GetFinalDamageWithScaling(SpellCastContext context, SpellScalingResult scalingResult)
    {
        float baseDamage = scalingResult.primaryPropertyValue;

        switch (context.commandUsed)
        {
            case SpellCommandType.DIRECTIONAL:
                if (context.commandIntensity >= 1f)
                {
                    baseDamage *= 1.1f; // Bonus por precisi�n
                }
                else
                {
                    baseDamage *= 0.95f; // Penalizaci�n por timeout
                }
                break;

            case SpellCommandType.EMERGE:
            case SpellCommandType.DESCEND:
                float intensityBonus = context.commandIntensity * gestureIntensityDamageMultiplier;
                baseDamage += intensityBonus;
                break;
        }

        return baseDamage;
    }

    private float GetFinalRadiusWithScaling(float scale)
    {
        // El radio escala con una curva personalizable
        if (explosionRadiusCurve != null && explosionRadiusCurve.length > 0)
        {
            return baseExplosionRadius * explosionRadiusCurve.Evaluate(scale);
        }

        // Fallback: escalado lineal
        return baseExplosionRadius * scale;
    }

    private Vector3 GetLaunchDirection(OriginData origin, SpellCastContext context)
    {
        switch (OriginConfig.originType)
        {
            case SpellOriginType.STAFF_TIP:
                if (context.hasValidTarget && context.commandUsed == SpellCommandType.DIRECTIONAL)
                {
                    return (context.targetPosition - origin.position).normalized;
                }
                else
                {
                    return origin.rotation * Vector3.forward;
                }

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
                return Vector3.up * 0.1f; // Velocidad m�nima

            default:
                if (context.hasValidTarget)
                {
                    return (context.targetPosition - origin.position).normalized;
                }
                return origin.rotation * Vector3.forward;
        }
    }
}