/// <summary>
/// Define los diferentes tipos de origen desde donde pueden aparecer los hechizos
/// </summary>
public enum SpellOriginType
{
    [System.ComponentModel.Description("Desde la punta del bastón")]
    STAFF_TIP,

    [System.ComponentModel.Description("Desde el centro del jugador (altura del pecho)")]
    PLAYER_CENTER,

    [System.ComponentModel.Description("Desde los pies del jugador")]
    PLAYER_FEET,

    [System.ComponentModel.Description("2-3 metros delante del jugador")]
    PLAYER_FRONT,

    [System.ComponentModel.Description("En el punto exacto donde apunta el bastón")]
    TARGET_POINT,

    [System.ComponentModel.Description("X metros encima del punto objetivo")]
    TARGET_ABOVE,

    [System.ComponentModel.Description("En la superficie del objetivo")]
    TARGET_SURFACE,

    [System.ComponentModel.Description("Punto fijo en el mundo (coordenadas específicas)")]
    WORLD_FIXED
}