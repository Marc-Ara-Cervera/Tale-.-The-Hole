using UnityEngine;
using System.Collections;

/// <summary>
/// Componente que maneja el comportamiento de un proyectil de bola de fuego
/// Detecta colisiones, aplica daño y crea efectos de explosión
/// </summary>
public class FireballProjectile : MonoBehaviour
{
    [Header("Configuración de Daño")]
    private float damage = 25f;
    private float explosionRadius = 3f;
    private bool hasExploded = false;

    [Header("Efectos")]
    [SerializeField] private GameObject explosionEffectPrefab;
    [SerializeField] private AudioClip explosionSound;

    /// <summary>
    /// Inicializa el proyectil con sus parámetros de daño
    /// </summary>
    /// <param name="projectileDamage">Cantidad de daño a causar</param>
    /// <param name="radius">Radio de la explosión</param>
    public void Initialize(float projectileDamage, float radius)
    {
        damage = projectileDamage;
        explosionRadius = radius;
        hasExploded = false;

        // Asegurarse de que tiene un Rigidbody para la física
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }

        // Asegurarse de que tiene un Collider para detectar colisiones
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            SphereCollider sphereCol = gameObject.AddComponent<SphereCollider>();
            sphereCol.isTrigger = true;
            sphereCol.radius = 0.2f;
        }
    }

    /// <summary>
    /// Detecta colisiones y explota al impactar
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        // Evitar explotar múltiples veces
        if (hasExploded) return;

        // Ignorar el jugador que lanzó el proyectil (opcional)
        if (other.CompareTag("Player")) return;

        // Explotar al tocar cualquier cosa sólida
        Explode();
    }

    /// <summary>
    /// También detecta colisiones no-trigger
    /// </summary>
    private void OnCollisionEnter(Collision collision)
    {
        if (hasExploded) return;

        // Ignorar el jugador que lanzó el proyectil (opcional)
        if (collision.gameObject.CompareTag("Player")) return;

        Explode();
    }

    /// <summary>
    /// Ejecuta la explosión del proyectil
    /// </summary>
    private void Explode()
    {
        if (hasExploded) return;
        hasExploded = true;

        Vector3 explosionPosition = transform.position;

        // Crear efecto visual de explosión
        CreateExplosionEffect(explosionPosition);

        // Reproducir sonido de explosión
        PlayExplosionSound(explosionPosition);

        // Aplicar daño en área
        ApplyExplosionDamage(explosionPosition);

        // Destruir el proyectil
        Destroy(gameObject);
    }

    /// <summary>
    /// Crea el efecto visual de la explosión
    /// </summary>
    private void CreateExplosionEffect(Vector3 position)
    {
        if (explosionEffectPrefab != null)
        {
            GameObject explosion = Instantiate(explosionEffectPrefab, position, Quaternion.identity);

            // Destruir el efecto después de un tiempo
            ParticleSystem ps = explosion.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                float duration = ps.main.duration + ps.main.startLifetime.constantMax;
                Destroy(explosion, duration);
            }
            else
            {
                Destroy(explosion, 3f);
            }
        }
        else
        {
            // Efecto básico si no hay prefab asignado
            Debug.Log($"¡EXPLOSIÓN! en {position} - Radio: {explosionRadius}m, Daño: {damage}");
        }
    }

    /// <summary>
    /// Reproduce el sonido de explosión
    /// </summary>
    private void PlayExplosionSound(Vector3 position)
    {
        if (explosionSound != null)
        {
            AudioSource.PlayClipAtPoint(explosionSound, position);
        }
    }

    /// <summary>
    /// Aplica daño a todos los objetivos en el radio de explosión
    /// </summary>
    private void ApplyExplosionDamage(Vector3 explosionCenter)
    {
        // Encontrar todos los colliders en el radio de explosión
        Collider[] hitColliders = Physics.OverlapSphere(explosionCenter, explosionRadius);

        foreach (Collider hitCollider in hitColliders)
        {
            // Verificar si el objeto puede recibir daño
            iDañable damageable = hitCollider.GetComponent<iDañable>();
            if (damageable != null)
            {
                // Calcular distancia para daño escalado (opcional)
                float distance = Vector3.Distance(explosionCenter, hitCollider.transform.position);
                float damageMultiplier = 1f - (distance / explosionRadius);
                damageMultiplier = Mathf.Clamp01(damageMultiplier);

                float finalDamage = damage * damageMultiplier;

                // Aplicar el daño
                damageable.TakeDamage(finalDamage, gameObject);

                Debug.Log($"Daño aplicado a {hitCollider.name}: {finalDamage} (distancia: {distance:F1}m)");
            }
        }
    }

    /// <summary>
    /// Destruye automáticamente el proyectil después de un tiempo si no explota
    /// </summary>
    private void Start()
    {
        // Auto-destrucción después de 10 segundos para evitar acumulación
        StartCoroutine(AutoDestroy());
    }

    /// <summary>
    /// Corrutina para auto-destrucción
    /// </summary>
    private IEnumerator AutoDestroy()
    {
        yield return new WaitForSeconds(10f);

        if (!hasExploded)
        {
            Debug.Log("Proyectil auto-destruido por timeout");
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Visualización del radio de explosión en el editor
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}