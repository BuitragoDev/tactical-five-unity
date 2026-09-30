using System.Collections.Generic;
using System.Linq;

/// <summary>Familia de hito de carrera (para calcular el progreso del siguiente hito).</summary>
public enum MilestoneFamily
{
    Points, Rebounds, Assists, Steals, Blocks, ThreePointers,
    Games, DoubleDoubles, TripleDoubles, AllStar, SeasonMvp, Rings, FinalsMvp
}

/// <summary>Definición de un hito de carrera.</summary>
public class MilestoneDef
{
    /// <summary>Clave estable (`points_10000`, `allstar_10`…) que se guarda en `player_milestones`.</summary>
    public string key;
    public MilestoneFamily family;
    public int threshold;
    /// <summary>Hito redondo (20.000 puntos, 10× All-Star…): noticia destacada y efecto mayor.</summary>
    public bool major;
}

/// <summary>Totales de carrera de un jugador (histórico + temporada en curso + honores).</summary>
public struct CareerTotals
{
    public int points, rebounds, assists, steals, blocks, threePointers;
    public int games, doubleDoubles, tripleDoubles;
    public int allStars, seasonMvps, rings, finalsMvps;
}

/// <summary>
/// Catálogo de hitos de legado (E1). Umbrales de carrera con sabor NBA; los `major` son los
/// redondos que se celebran con más fuerza. Puro: lo consumen `MilestoneHelper`, el servicio
/// de detección diaria y la UI (Trayectoria/Perfil).
/// </summary>
public static class MilestoneCatalog
{
    public static readonly List<MilestoneDef> All = new()
    {
        // Puntos
        Def(MilestoneFamily.Points, 10000), Def(MilestoneFamily.Points, 15000),
        Def(MilestoneFamily.Points, 20000, major: true), Def(MilestoneFamily.Points, 25000),
        Def(MilestoneFamily.Points, 30000, major: true),
        // Rebotes
        Def(MilestoneFamily.Rebounds, 5000), Def(MilestoneFamily.Rebounds, 10000, major: true),
        Def(MilestoneFamily.Rebounds, 15000, major: true),
        // Asistencias
        Def(MilestoneFamily.Assists, 2500), Def(MilestoneFamily.Assists, 5000, major: true),
        Def(MilestoneFamily.Assists, 7500), Def(MilestoneFamily.Assists, 10000, major: true),
        // Robos / tapones / triples
        Def(MilestoneFamily.Steals, 1000), Def(MilestoneFamily.Steals, 2000, major: true),
        Def(MilestoneFamily.Blocks, 500), Def(MilestoneFamily.Blocks, 1000, major: true),
        Def(MilestoneFamily.Blocks, 2000, major: true),
        Def(MilestoneFamily.ThreePointers, 1000), Def(MilestoneFamily.ThreePointers, 2000, major: true),
        Def(MilestoneFamily.ThreePointers, 3000, major: true),
        // Partidos y dobles
        Def(MilestoneFamily.Games, 500), Def(MilestoneFamily.Games, 1000, major: true),
        Def(MilestoneFamily.DoubleDoubles, 200), Def(MilestoneFamily.DoubleDoubles, 400, major: true),
        Def(MilestoneFamily.TripleDoubles, 20), Def(MilestoneFamily.TripleDoubles, 50, major: true),
        // Honores
        Def(MilestoneFamily.AllStar, 3), Def(MilestoneFamily.AllStar, 5, major: true),
        Def(MilestoneFamily.AllStar, 10, major: true),
        Def(MilestoneFamily.SeasonMvp, 1), Def(MilestoneFamily.SeasonMvp, 2, major: true),
        Def(MilestoneFamily.SeasonMvp, 3, major: true),
        Def(MilestoneFamily.Rings, 1), Def(MilestoneFamily.Rings, 2, major: true),
        Def(MilestoneFamily.Rings, 3, major: true),
        Def(MilestoneFamily.FinalsMvp, 1), Def(MilestoneFamily.FinalsMvp, 2, major: true),
    };

    public static MilestoneDef Def(MilestoneFamily family, int threshold, bool major = false)
        => new MilestoneDef { key = KeyOf(family, threshold), family = family, threshold = threshold, major = major };

    public static string KeyOf(MilestoneFamily family, int threshold) => $"{Prefix(family)}_{threshold}";

    public static string Prefix(MilestoneFamily family) => family switch
    {
        MilestoneFamily.Points => "points",
        MilestoneFamily.Rebounds => "rebounds",
        MilestoneFamily.Assists => "assists",
        MilestoneFamily.Steals => "steals",
        MilestoneFamily.Blocks => "blocks",
        MilestoneFamily.ThreePointers => "threes",
        MilestoneFamily.Games => "games",
        MilestoneFamily.DoubleDoubles => "dd",
        MilestoneFamily.TripleDoubles => "td",
        MilestoneFamily.AllStar => "allstar",
        MilestoneFamily.SeasonMvp => "mvp",
        MilestoneFamily.Rings => "rings",
        _ => "finalsmvp"
    };

    public static List<MilestoneDef> OfFamily(MilestoneFamily family)
        => All.Where(d => d.family == family).OrderBy(d => d.threshold).ToList();

    public static MilestoneDef ByKey(string key)
        => string.IsNullOrEmpty(key) ? null : All.FirstOrDefault(d => d.key == key);

    public static string Label(MilestoneDef d) => d.family switch
    {
        MilestoneFamily.Points => $"{d.threshold:N0} puntos",
        MilestoneFamily.Rebounds => $"{d.threshold:N0} rebotes",
        MilestoneFamily.Assists => $"{d.threshold:N0} asistencias",
        MilestoneFamily.Steals => $"{d.threshold:N0} robos",
        MilestoneFamily.Blocks => $"{d.threshold:N0} tapones",
        MilestoneFamily.ThreePointers => $"{d.threshold:N0} triples",
        MilestoneFamily.Games => $"{d.threshold:N0} partidos",
        MilestoneFamily.DoubleDoubles => $"{d.threshold:N0} dobles-dobles",
        MilestoneFamily.TripleDoubles => $"{d.threshold:N0} triples-dobles",
        MilestoneFamily.AllStar => $"{d.threshold}× All-Star",
        MilestoneFamily.SeasonMvp => d.threshold == 1 ? "MVP de la temporada" : $"{d.threshold}× MVP de la temporada",
        MilestoneFamily.Rings => d.threshold == 1 ? "1 anillo" : $"{d.threshold} anillos",
        MilestoneFamily.FinalsMvp => d.threshold == 1 ? "MVP de las Finales" : $"{d.threshold}× MVP de las Finales",
        _ => d.threshold.ToString()
    };

    /// <summary>Icono (carpeta `Resources/Icons`) para la UI, reutilizando el arte existente.</summary>
    public static string Icon(MilestoneDef d) => "Icons/" + (d.family switch
    {
        MilestoneFamily.Points => "puntos",
        MilestoneFamily.Rebounds => "rebote",
        MilestoneFamily.Assists => "asistencia_icon",
        MilestoneFamily.Steals => "robo",
        MilestoneFamily.Blocks => "tapones",
        MilestoneFamily.ThreePointers => "tiro",
        MilestoneFamily.Games => "calendario",
        MilestoneFamily.DoubleDoubles => "estadisticas",
        MilestoneFamily.TripleDoubles => "estadisticas",
        MilestoneFamily.AllStar => "estrella64px",
        MilestoneFamily.SeasonMvp => "trofeo_mvp",
        MilestoneFamily.Rings => "trofeo",
        _ => "mvp_finals"
    });

    /// <summary>Icono genérico de "hito" (cabecera de la sección).</summary>
    public const string SectionIcon = "Icons/record_icon";
}
