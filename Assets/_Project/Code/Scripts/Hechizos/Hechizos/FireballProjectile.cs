using UnityEngine;
using System.Collections;

/// <summary>
/// Componente que maneja el comportamiento de un proyectil de bola de fuego
/// Detecta colisiones, aplica da�o y crea efectos de explosi�n
/// </summary>
public class FireballProjectile : MonoBehaviour
{
    [Header("Configuraci�n de Da�o")]
    private float damage = 25f;
    private float explosionRadius = 3f;
    private bool hasExploded = false;

    [Header("Efectos")]
    [SerializeField] private GameObject explosionEffectPrefab;
    [SerializeField] private AudioClip explosionSound;

    /// <summary>
    /// Inicializa el proyectil con sus par�metros de da�o
    /// </summary>
    /// <param name="projectileDamage">Cantidad de da�o a causar</param>
    /// <param name="radius">Radio de la explosi�n</param>
    public void Initialize(float projectileDamage, float radius)
    {
        damage = projectileDamage;
        explosionRadius = radius;
        hasExploded = false;

        // Asegurarse de que tiene un Rigidbody para la f�sica
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
        // Evitar explotar m�ltiples veces
        if (hasExploded) return;

        // Ignorar el jugador que lanz� el proyectil (opcional)
        if (other.CompareTag("Player")) return;

        // Explotar al tocar cualquier cosa s�lida
        Explode();
    }

    /// <summary>
    /// Tambi�n detecta colisiones no-trigger
    /// </summary>
    private void OnCollisionEnter(Collision collision)
    {
        if (hasExploded) return;

        // Ignorar el jugador que lanz� el proyectil (opcional)
        if (collision.gameObject.CompareTag("Player")) return;

        Explode();
    }

    /// <summary>
    /// Ejecuta la explosi�n del proyectil
    /// </summary>
    private void Explode()
    {
        if (hasExploded) return;
        hasExploded = true;

        Vector3 explosionPosition = transform.position;

        // Crear efecto visual de explosi�n
        CreateExplosionEffect(explosionPosition);

        // Reproducir sonido de explosi�n
        PlayExplosionSound(explosionPosition);

        // Aplicar da�o en �rea
        ApplyExplosionDamage(explosionPosition);

        // Destruir el proyectil
        Destroy(gameObject);
    }

    /// <summary>
    /// Crea el efecto visual de la explosi�n
    /// </summary>
    private void CreateExplosionEffect(Vector3 position)
    {
        if (explosionEffectPrefab != null)
        {
            GameObject explosion = Instantiate(explosionEffectPrefab, position, Quaternion.identity);

            // Destruir el efecto despu�s de un tiempo
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
            // Efecto b�sico si no hay prefab asignado
            Debug.Log($"�EXPLOSI�N! en {position} - Radio: {explosionRadius}m, Da�o: {damage}");
        }
    }

    /// <summary>
    /// Reproduce el sonido de explosi�n
    /// </summary>
    private void PlayExplosionSound(Vector3 position)
    {
        if (explosionSound != null)
        {
            AudioSource.PlayClipAtPoint(explosionSound, position);
        }
    }

    /// <summary>
    /// Aplica da�o a todos los objetivos en el radio de explosi�n
    /// </summary>
    private void ApplyExplosionDamage(Vector3 explosionCenter)
    {
        // Encontrar todos los colliders en el radio de explosi�n
        Collider[] hitColliders = Physics.OverlapSphere(explosionCenter, explosionRadius);

        foreach (Collider hitCollider in hitColliders)
        {
            // Verificar si el objeto puede recibir da�o
            iDa�able damageable = hitCollider.GetComponent<iDa�able>();
            if (damageable != null)
            {
                // Calcular distancia para da�o escalado (opcional)
                float distance = Vector3.Distance(explosionCenter, hitCollider.transform.position);
                float damageMultiplier = 1f - (distance / explosionRadius);
                damageMultiplier = Mathf.Clamp01(damageMultiplier);

                float finalDamage = damage * damageMultiplier;

                // Aplicar el da�o
                damageable.TakeDamage(finalDamage, gameObject);

                Debug.Log($"Da�o aplicado a {hitCollider.name}: {finalDamage} (distancia: {distance:F1}m)");
            }
        }
    }

    /// <summary>
    /// Destruye autom�ticamente el proyectil despu�s de un tiempo si no explota
    /// </summary>
    private void Start()
    {
        // Auto-destrucci�n despu�s de 10 segundos para evitar acumulaci�n
        StartCoroutine(AutoDestroy());
    }

    /// <summary>
    /// Corrutina para auto-destrucci�n
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
    /// Visualizaci�n del radio de explosi�n en el editor
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}