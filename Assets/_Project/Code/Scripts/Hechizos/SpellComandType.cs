/// <summary>
/// Define los diferentes tipos de comandos que requieren los hechizos despu�s de ser preparados
/// </summary>
public enum SpellCommandType
{
    [System.ComponentModel.Description("Apuntar hacia el objetivo durante unos segundos")]
    DIRECTIONAL,

    [System.ComponentModel.Description("Gesto de bast�n hacia arriba (invocar desde el suelo)")]
    EMERGE,

    [System.ComponentModel.Description("Gesto de bast�n hacia abajo (invocar desde el cielo)")]
    DESCEND,

    [System.ComponentModel.Description("Sin comando adicional, se ejecuta inmediatamente")]
    INSTANT
}