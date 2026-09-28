using System.Globalization;

/// <summary>
/// Formato único para las cifras de estadísticas (punto decimal: 24.3) y textos de ayuda.
/// Evita la mezcla es-ES/Invariant entre pantallas (QA-10): el perfil usaba coma y Stats
/// punto para el mismo dato.
/// </summary>
public static class StatFormat
{
    /// <summary>Cultura de cifras de stats: punto decimal y de millares, fija en cualquier idioma.</summary>
    public static readonly CultureInfo Culture = CreateDotCulture();

    static CultureInfo CreateDotCulture()
    {
        var ci = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        ci.NumberFormat.NumberGroupSeparator = ".";
        ci.NumberFormat.NumberDecimalSeparator = ".";
        return ci;
    }

    public static string N0(int value) => value.ToString("N0", Culture);
    public static string One(float value) => value.ToString("N1", Culture);
    public static string Two(float value) => value.ToString("N2", Culture);

    // ── Tooltips de estadísticas avanzadas (UI) ──
    public const string EfgTip =
        "eFG% (tiro efectivo): (canastas + 0.5 × triples) / intentos de campo. " +
        "Da más valor al triple que el TC% normal.";
    public const string TsTip =
        "TS% (tiro real): puntos / (2 × (intentos de campo + 0.44 × tiros libres)). " +
        "Mide la eficiencia anotadora total, tiros libres incluidos.";
    public const string PerTip =
        "PER (valoración por minuto): (puntos + rebotes + asistencias + robos + tapones − fallos − pérdidas) " +
        "normalizado por minutos jugados.";
    public const string ValTip =
        "VAL (valoración): puntos + rebotes + asistencias + robotes + tapones − fallos de campo y libres − pérdidas.";
    public const string TovTip = "Pérdidas de balón por partido.";
    public const string MinTip = "Minutos jugados por partido.";
}
