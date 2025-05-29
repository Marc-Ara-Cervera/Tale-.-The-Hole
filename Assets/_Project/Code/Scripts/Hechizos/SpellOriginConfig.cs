using UnityEngine;

/// <summary>
/// Configuración completa para el origen de un hechizo
/// </summary>
[System.Serializable]
public class SpellOriginConfig
{
    [Header("Tipo de Origen")]
    [Tooltip("Desde dónde se origina el hechizo")]
    public SpellOriginType originType = SpellOriginType.STAFF_TIP;

    [Header("Offsets y Ajustes")]
    [Tooltip("Desplazamiento adicional en las coordenadas locales del origen")]
    public Vector3 positionOffset = Vector3.zero;

    [Tooltip("Rotación adicional en grados")]
    public Vector3 rotationOffset = Vector3.zero;

    [Header("Configuraciones Específicas")]
    [Tooltip("Altura sobre el objetivo (solo para TARGET_ABOVE)")]
    [SerializeField] private float heightAboveTarget = 10f;

    [Tooltip("Distancia delante del jugador (solo para PLAYER_FRONT)")]
    [SerializeField] private float distanceInFrontOfPlayer = 2.5f;

    [Tooltip("Coordenadas fijas del mundo (solo para WORLD_FIXED)")]
    [SerializeField] private Vector3 worldFixedPosition = Vector3.zero;

    // Getters públicos para acceso controlado
    public float HeightAboveTarget => heightAboveTarget;
    public float DistanceInFrontOfPlayer => distanceInFrontOfPlayer;
    public Vector3 WorldFixedPosition => worldFixedPosition;

    /// <summary>
    /// Constructor por defecto
    /// </summary>
    public SpellOriginConfig()
    {
        originType = SpellOriginType.STAFF_TIP;
        positionOffset = Vector3.zero;
        rotationOffset = Vector3.zero;
    }

    /// <summary>
    /// Constructor con tipo específico
    /// </summary>
    public SpellOriginConfig(SpellOriginType type)
    {
        originType = type;
        positionOffset = Vector3.zero;
        rotationOffset = Vector3.zero;

        // Configuraciones por defecto según el tipo
        switch (type)
        {
            case SpellOriginType.TARGET_ABOVE:
                heightAboveTarget = 10f;
                break;
            case SpellOriginType.PLAYER_FRONT:
                distanceInFrontOfPlayer = 2.5f;
                break;
        }
    }

    /// <summary>
    /// Crea una copia de esta configuración
    /// </summary>
    public SpellOriginConfig Clone()
    {
        SpellOriginConfig clone = new SpellOriginConfig(originType);
        clone.positionOffset = positionOffset;
        clone.rotationOffset = rotationOffset;
        clone.heightAboveTarget = heightAboveTarget;
        clone.distanceInFrontOfPlayer = distanceInFrontOfPlayer;
        clone.worldFixedPosition = worldFixedPosition;
        return clone;
    }
}