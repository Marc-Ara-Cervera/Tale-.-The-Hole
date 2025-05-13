using UnityEngine;

/// <summary>
/// Hechizo b�sico de bola de fuego.
/// Crea un proyectil que avanza en l�nea recta y causa da�o a los enemigos.
/// </summary>
[CreateAssetMenu(fileName = "New Fireball Spell", menuName = "Spells/Fireball")]
public class FireballSpell : SpellBase
{
    [Header("Configuraci�n de Bola de Fuego")]
    [Tooltip("Da�o base que causa el proyectil")]
    [SerializeField] private float baseDamage = 20f;

    [Tooltip("Velocidad del proyectil")]
    [SerializeField] private float projectileSpeed = 15f;

    [Tooltip("Tiempo de vida del proyectil en segundos")]
    [SerializeField] private float projectileLifetime = 5f;

    [Tooltip("Radio de da�o en impacto (0 para solo da�o directo)")]
    [SerializeField] private float explosionRadius = 0f;

    [Tooltip("Fuerza de impacto f�sica")]
    [SerializeField] private float impactForce = 10f;

    [Tooltip("Prefab para el efecto de impacto")]
    [SerializeField] private GameObject impactEffectPrefab;

    /// <summary>
    /// Implementaci�n espec�fica del efecto de bola de fuego
    /// </summary>
    protected override void ExecuteSpellEffect(Transform origin, PlayerStatsManager caster)
    {
        // Crear la instancia del proyectil
        GameObject fireballInstance = Instantiate(spellPrefab, origin.position, origin.rotation);

        // Configurar el proyectil de bola de fuego
        FireballProjectile projectile = fireballInstance.GetComponent<FireballProjectile>();
        if (projectile != null)
        {
            // Pasar los par�metros al proyectil
            projectile.Initialize(baseDamage, caster.gameObject, projectileSpeed,
                                  projectileLifetime, explosionRadius, impactForce, impactEffectPrefab);
        }
        else
        {
            // Si el prefab no tiene el componente FireballProjectile, a�adir una f�sica b�sica
            Rigidbody rb = fireballInstance.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.velocity = origin.forward * projectileSpeed;

                // Destruir el proyectil despu�s del tiempo de vida
                Destroy(fireballInstance, projectileLifetime);
            }
            else
            {
                Debug.LogWarning("El prefab de bola de fuego no tiene Rigidbody ni FireballProjectile. No se mover� correctamente.");
            }
        }
    }
}