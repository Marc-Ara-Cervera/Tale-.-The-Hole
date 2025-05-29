using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ejemplo de hechizo que emerge del suelo (EMERGE command)
/// </summary>
[CreateAssetMenu(fileName = "Earth Wall Spell", menuName = "Spells/Earth/Earth Wall")]
public class EarthWallSpell : SpellBase
{
    [Header("Configuraci�n de Muro de Tierra")]
    [SerializeField] private float wallHeight = 5f;
    [SerializeField] private float wallWidth = 8f;
    [SerializeField] private float wallDuration = 30f;

    protected override void ExecuteSpellEffect(OriginData origin, SpellCastContext context)
    {
        Debug.Log($"Creando Muro de Tierra desde {OriginConfig.originType} con comando {context.commandUsed}");

        if (spellPrefab != null)
        {
            GameObject earthWall = Instantiate(spellPrefab, origin.position, origin.rotation);

            // Configurar el muro seg�n la intensidad del gesto
            ConfigureEarthWall(earthWall, context);

            // Auto-destruir despu�s de la duraci�n
            Destroy(earthWall, wallDuration);
        }
        else
        {
            Debug.Log($"�MURO DE TIERRA! En {origin.position} - Altura: {wallHeight}m");
        }
    }

    private void ConfigureEarthWall(GameObject wall, SpellCastContext context)
    {
        // Escalar el muro seg�n la intensidad del gesto
        float intensityMultiplier = 1f;
        if (context.commandUsed == SpellCommandType.EMERGE)
        {
            // M�s intensidad = muro m�s grande
            intensityMultiplier = Mathf.Clamp(context.commandIntensity / 2f, 0.7f, 1.5f);
        }

        Vector3 finalScale = new Vector3(wallWidth * intensityMultiplier, wallHeight * intensityMultiplier, 1f);
        wall.transform.localScale = finalScale;

        // Animar la emergencia del suelo
        AnimateEmergence(wall, finalScale);

        Debug.Log($"Muro configurado: Escala={finalScale}, Intensidad={context.commandIntensity}");
    }

    private void AnimateEmergence(GameObject wall, Vector3 targetScale)
    {
        // Comenzar desde escala peque�a
        wall.transform.localScale = new Vector3(targetScale.x, 0.1f, targetScale.z);

        wall.transform.localScale = targetScale;
    }
}