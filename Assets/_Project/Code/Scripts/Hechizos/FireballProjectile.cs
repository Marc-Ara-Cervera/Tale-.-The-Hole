using UnityEngine;
using System.Collections;

public class FireballProjectile : MonoBehaviour
{
    // Propiedades del proyectil
    private float damage;
    private GameObject caster;
    private float speed;
    private float lifetime;
    private float explosionRadius;
    private float impactForce;
    private GameObject impactEffectPrefab;

    // Componentes
    private Rigidbody rb;
    private Collider projCollider;

    // Para seguimiento del tiempo de vida
    private float spawnTime;

    // Capa de colisi�n del lanzador para ignorarla
    private int casterLayer;
    private int casterLayerMask;

    // Flag para control de colisiones
    private bool canCollide = false;

    private void Awake()
    {
        // Obtener o a�adir el Rigidbody
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }

        // Configurar el Rigidbody para comportamiento de proyectil
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.constraints = RigidbodyConstraints.FreezeRotation; // Opcional: evita que gire el proyectil

        // Obtener el collider
        projCollider = GetComponent<Collider>();
        if (projCollider == null)
        {
            // A�adir un SphereCollider si no tiene collider
            projCollider = gameObject.AddComponent<SphereCollider>();
            ((SphereCollider)projCollider).radius = 0.2f; // Ajustar seg�n el tama�o del modelo
        }

        // Configurar el collider como trigger para detecci�n de colisiones
        projCollider.isTrigger = true;
    }

    /// <summary>
    /// Configura las propiedades del proyectil
    /// </summary>
    public void Initialize(float damage, GameObject caster, float speed, float lifetime,
                          float explosionRadius = 0f, float impactForce = 0f,
                          GameObject impactEffectPrefab = null)
    {
        this.damage = damage;
        this.caster = caster;
        this.speed = speed;
        this.lifetime = lifetime;
        this.explosionRadius = explosionRadius;
        this.impactForce = impactForce;
        this.impactEffectPrefab = impactEffectPrefab;

        // Registrar tiempo de creaci�n
        spawnTime = Time.time;

        // Guardar la capa del lanzador para ignorar colisiones
        if (caster != null)
        {
            casterLayer = caster.layer;
            casterLayerMask = 1 << casterLayer;

            // Ignorar colisiones f�sicas con el lanzador
            Collider casterCollider = caster.GetComponent<Collider>();
            if (casterCollider != null && projCollider != null)
            {
                Physics.IgnoreCollision(projCollider, casterCollider, true);
            }
        }

        // Configurar velocidad
        if (rb != null)
        {
            rb.velocity = transform.forward * speed;
        }

        // Activar colisiones despu�s de un breve retraso para evitar colisiones inmediatas
        StartCoroutine(EnableCollisionsAfterDelay(0.1f));
    }

    /// <summary>
    /// Habilita las colisiones despu�s de un breve retraso para evitar colisiones con el lanzador
    /// </summary>
    private IEnumerator EnableCollisionsAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        canCollide = true;

        // Activar un efecto de part�culas si existe
        ParticleSystem particleSystem = GetComponentInChildren<ParticleSystem>();
        if (particleSystem != null && !particleSystem.isPlaying)
        {
            particleSystem.Play();
        }
    }

    private void Update()
    {
        // Comprobar si ha excedido su tiempo de vida
        if (Time.time - spawnTime >= lifetime)
        {
            DestroyProjectile();
            return;
        }

        // Opcional: A�adir efectos visuales durante el vuelo
        // Por ejemplo, dejar un rastro de part�culas o ajustar el tama�o/color
    }

    private void OnTriggerEnter(Collider other)
    {
        // Ignorar colisiones hasta que se active el flag
        if (!canCollide)
            return;

        // Ignorar colisiones con el lanzador
        if (other.gameObject == caster || other.transform.IsChildOf(caster.transform))
            return;

        // Ignorar colisiones con otros proyectiles
        if (other.GetComponent<FireballProjectile>() != null)
            return;

        // Debug para ver con qu� est� colisionando
        Debug.Log($"Bola de fuego impact� con: {other.gameObject.name} en la capa {LayerMask.LayerToName(other.gameObject.layer)}");

        // Aplicar da�o al objeto impactado
        ApplyDamage(other.gameObject);

        // Aplicar da�o de �rea si corresponde
        if (explosionRadius > 0)
        {
            ApplyExplosionDamage();
        }

        // Efecto de impacto
        if (impactEffectPrefab != null)
        {
            Instantiate(impactEffectPrefab, transform.position, Quaternion.identity);
        }

        // Destruir el proyectil
        DestroyProjectile();
    }

    /// <summary>
    /// Aplica da�o al objetivo impactado
    /// </summary>
    private void ApplyDamage(GameObject target)
    {
        // Buscar la interfaz IDa�able en el objeto impactado o en sus padres
        iDa�able damageable = target.GetComponent<iDa�able>();
        if (damageable == null)
        {
            // Intentar encontrar en los padres (�til si el collider est� en un hijo)
            damageable = target.GetComponentInParent<iDa�able>();
        }

        if (damageable != null)
        {
            damageable.TakeDamage(damage, caster);
        }

        // Aplicar fuerza f�sica si tiene Rigidbody
        Rigidbody targetRb = target.GetComponent<Rigidbody>();
        if (targetRb != null && impactForce > 0)
        {
            Vector3 direction = (target.transform.position - transform.position).normalized;
            targetRb.AddForce(direction * impactForce, ForceMode.Impulse);
        }
    }

    /// <summary>
    /// Aplica da�o en �rea por explosi�n
    /// </summary>
    private void ApplyExplosionDamage()
    {
        // Encontrar todos los colliders en el radio de explosi�n
        Collider[] colliders = Physics.OverlapSphere(transform.position, explosionRadius);

        foreach (Collider hit in colliders)
        {
            // Ignorar al lanzador
            if (hit.gameObject == caster || hit.transform.IsChildOf(caster.transform))
                continue;

            // Calcular da�o basado en la distancia (m�s da�o cerca, menos lejos)
            float distance = Vector3.Distance(transform.position, hit.transform.position);
            float damagePercent = 1f - Mathf.Clamp01(distance / explosionRadius);
            float finalDamage = damage * damagePercent;

            // Aplicar da�o
            iDa�able damageable = hit.GetComponent<iDa�able>();
            if (damageable != null)
            {
                damageable.TakeDamage(finalDamage, caster);
            }

            // Aplicar fuerza explosiva si tiene Rigidbody
            if (impactForce > 0)
            {
                Rigidbody rb = hit.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    Vector3 direction = (hit.transform.position - transform.position).normalized;
                    float forceMagnitude = impactForce * damagePercent;
                    rb.AddForce(direction * forceMagnitude, ForceMode.Impulse);
                }
            }
        }
    }

    /// <summary>
    /// Destruye el proyectil y sus efectos
    /// </summary>
    private void DestroyProjectile()
    {
        // Aqu� podr�as a�adir alg�n efecto de desaparici�n antes de destruir
        Destroy(gameObject);
    }

    /// <summary>
    /// Para visualizar el radio de explosi�n en el editor
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (explosionRadius > 0)
        {
            Gizmos.color = new Color(1, 0, 0, 0.3f);
            Gizmos.DrawSphere(transform.position, explosionRadius);
        }
    }
}