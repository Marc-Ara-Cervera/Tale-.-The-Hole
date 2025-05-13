using UnityEngine;

/// <summary>
/// Hechizo básico de bola de fuego.
/// Crea un proyectil que avanza en línea recta y causa daño a los enemigos.
/// </summary>
[CreateAssetMenu(fileName = "New Fireball Spell", menuName = "Spells/Fireball")]
public class FireballSpell : SpellBase
{
    [Header("Configuración de Bola de Fuego")]
    [Tooltip("Daño base que causa el proyectil")]
    [SerializeField] private float baseDamage = 20f;

    [Tooltip("Velocidad del proyectil")]
    [SerializeField] private float projectileSpeed = 15f;

    [Tooltip("Tiempo de vida del proyectil en segundos")]
    [SerializeField] private float projectileLifetime = 5f;

    [Tooltip("Radio de daño en impacto (0 para solo daño directo)")]
    [SerializeField] private float explosionRadius = 0f;

    [Tooltip("Fuerza de impacto física")]
    [SerializeField] private float impactForce = 10f;

    [Tooltip("Prefab para el efecto de impacto")]
    [SerializeField] private GameObject impactEffectPrefab;

    /// <summary>
    /// Implementación específica del efecto de bola de fuego
    /// </summary>
    protected override void ExecuteSpellEffect(Transform origin, PlayerStatsManager caster)
    {
        // Crear la instancia del proyectil
        GameObject fireballInstance = Instantiate(spellPrefab, origin.position, origin.rotation);

        // Configurar el proyectil de bola de fuego
        FireballProjectile projectile = fireballInstance.GetComponent<FireballProjectile>();
        if (projectile != null)
        {
            // Pasar los parámetros al proyectil
            projectile.Initialize(baseDamage, caster.gameObject, projectileSpeed,
                                  projectileLifetime, explosionRadius, impactForce, impactEffectPrefab);
        }
        else
        {
            // Si el prefab no tiene el componente FireballProjectile, añadir una física básica
            Rigidbody rb = fireballInstance.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.velocity = origin.forward * projectileSpeed;

                // Destruir el proyectil después del tiempo de vida
                Destroy(fireballInstance, projectileLifetime);
            }
            else
            {
                Debug.LogWarning("El prefab de bola de fuego no tiene Rigidbody ni FireballProjectile. No se moverá correctamente.");
            }
        }
    }
}