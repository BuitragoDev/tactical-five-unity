using System.Collections.Generic;
using System.Linq;

/// <summary>Gravedad de una lesión: define el pronóstico, el parte médico y el riesgo de recaída.</summary>
public enum InjurySeverity { Leve = 0, Moderada = 1, Grave = 2, Critica = 3 }

/// <summary>Definición de un tipo de lesión (catálogo estático y puro).</summary>
public class InjuryDef
{
    public string name;
    public string family;        // agrupa lesiones que pueden recaer en una peor (rodilla, muscular…)
    public int minDays;
    public int maxDays;
    public int weight;           // peso relativo en el sorteo
    public InjurySeverity severity;
    public int relapseRisk;      // 0-100: prob. base de recaída si el jugador vuelve antes de tiempo
}

/// <summary>
/// Catálogo de lesiones (E2). Puro: sin estado ni aleatoriedad propia (el sorteo recibe
/// el `roll`), para poder testearlo y para que lo usen tanto el simulador como la UI.
/// </summary>
public static class InjuryCatalog
{
    public static readonly List<InjuryDef> All = new()
    {
        // ── Muy comunes (leves) ──
        new InjuryDef { name = "Sobrecarga muscular",  family = "muscular", minDays = 1,  maxDays = 3,   weight = 60, severity = InjurySeverity.Leve,     relapseRisk = 8  },
        new InjuryDef { name = "Calambres musculares", family = "muscular", minDays = 1,  maxDays = 2,   weight = 55, severity = InjurySeverity.Leve,     relapseRisk = 5  },
        new InjuryDef { name = "Golpe / contusión leve", family = "hueso",  minDays = 1,  maxDays = 4,   weight = 50, severity = InjurySeverity.Leve,     relapseRisk = 6  },
        new InjuryDef { name = "Esguince leve de tobillo", family = "tobillo", minDays = 3, maxDays = 7, weight = 40, severity = InjurySeverity.Leve,     relapseRisk = 12 },

        // ── Comunes (moderadas) ──
        new InjuryDef { name = "Distensión muscular",  family = "muscular", minDays = 5,  maxDays = 10,  weight = 35, severity = InjurySeverity.Moderada, relapseRisk = 15 },
        new InjuryDef { name = "Tendinitis leve",      family = "tendon",   minDays = 4,  maxDays = 8,   weight = 30, severity = InjurySeverity.Moderada, relapseRisk = 18 },
        new InjuryDef { name = "Lumbalgia",            family = "espalda",  minDays = 3,  maxDays = 8,   weight = 28, severity = InjurySeverity.Moderada, relapseRisk = 20 },
        new InjuryDef { name = "Esguince moderado de tobillo", family = "tobillo", minDays = 8, maxDays = 14, weight = 25, severity = InjurySeverity.Moderada, relapseRisk = 18 },
        new InjuryDef { name = "Contusión ósea",       family = "hueso",    minDays = 10, maxDays = 21,  weight = 20, severity = InjurySeverity.Moderada, relapseRisk = 12 },
        new InjuryDef { name = "Tendinitis rotuliana", family = "tendon",   minDays = 7,  maxDays = 14,  weight = 18, severity = InjurySeverity.Moderada, relapseRisk = 22 },
        new InjuryDef { name = "Fascitis plantar",     family = "tendon",   minDays = 7,  maxDays = 15,  weight = 15, severity = InjurySeverity.Moderada, relapseRisk = 20 },
        new InjuryDef { name = "Esguince de muñeca",   family = "mano",     minDays = 7,  maxDays = 14,  weight = 15, severity = InjurySeverity.Moderada, relapseRisk = 15 },
        new InjuryDef { name = "Lesión en el gemelo",  family = "muscular", minDays = 10, maxDays = 18,  weight = 14, severity = InjurySeverity.Moderada, relapseRisk = 25 },
        new InjuryDef { name = "Contractura muscular", family = "muscular", minDays = 5,  maxDays = 12,  weight = 14, severity = InjurySeverity.Moderada, relapseRisk = 15 },

        // ── Poco frecuentes ──
        new InjuryDef { name = "Rotura fibrilar leve", family = "muscular", minDays = 14, maxDays = 21,  weight = 12, severity = InjurySeverity.Grave,    relapseRisk = 30 },
        new InjuryDef { name = "Subluxación de dedo",  family = "mano",     minDays = 10, maxDays = 18,  weight = 10, severity = InjurySeverity.Moderada, relapseRisk = 12 },
        new InjuryDef { name = "Esguince grave de tobillo", family = "tobillo", minDays = 14, maxDays = 28, weight = 8, severity = InjurySeverity.Grave,   relapseRisk = 28 },
        new InjuryDef { name = "Luxación de hombro",   family = "hombro",   minDays = 21, maxDays = 35,  weight = 7,  severity = InjurySeverity.Grave,    relapseRisk = 30 },
        new InjuryDef { name = "Fractura por estrés",  family = "hueso",    minDays = 21, maxDays = 35,  weight = 6,  severity = InjurySeverity.Grave,    relapseRisk = 20 },
        new InjuryDef { name = "Rotura fibrilar grave", family = "muscular", minDays = 21, maxDays = 42, weight = 5,  severity = InjurySeverity.Grave,    relapseRisk = 35 },

        // ── Graves ──
        new InjuryDef { name = "Rotura parcial de ligamentos", family = "rodilla", minDays = 28, maxDays = 65,  weight = 4, severity = InjurySeverity.Grave,  relapseRisk = 40 },
        new InjuryDef { name = "Rotura de ligamentos", family = "rodilla", minDays = 42, maxDays = 90,  weight = 3, severity = InjurySeverity.Critica, relapseRisk = 45 },
        new InjuryDef { name = "Rotura de menisco",    family = "rodilla",  minDays = 35, maxDays = 70,  weight = 3,  severity = InjurySeverity.Grave,    relapseRisk = 38 },
        new InjuryDef { name = "Fractura de mano",     family = "mano",     minDays = 30, maxDays = 66,  weight = 2,  severity = InjurySeverity.Grave,    relapseRisk = 15 },
        new InjuryDef { name = "Fractura de muñeca",   family = "mano",     minDays = 45, maxDays = 75,  weight = 2,  severity = InjurySeverity.Grave,    relapseRisk = 18 },
        new InjuryDef { name = "Rotura del tendón de Aquiles", family = "tendon", minDays = 120, maxDays = 240, weight = 1, severity = InjurySeverity.Critica, relapseRisk = 50 },
        new InjuryDef { name = "Rotura del ligamento cruzado anterior", family = "rodilla", minDays = 180, maxDays = 300, weight = 1, severity = InjurySeverity.Critica, relapseRisk = 55 },
    };

    /// <summary>Busca una lesión por su nombre exacto (para recaídas y saves antiguos).</summary>
    public static InjuryDef ByName(string name)
        => string.IsNullOrEmpty(name) ? null : All.FirstOrDefault(d => d.name == name);

    /// <summary>Lesiones de la misma familia con gravedad estrictamente mayor (candidatas a recaída).</summary>
    public static List<InjuryDef> WorsenCandidates(InjuryDef original)
    {
        if (original == null) return new List<InjuryDef>();
        var worse = All.Where(d => d.family == original.family && d.severity > original.severity).ToList();
        if (worse.Count == 0)
            worse = All.Where(d => d.severity > original.severity).ToList();
        return worse;
    }

    /// <summary>Gravedad estimada por días de baja (fallback para saves sin gravedad guardada).</summary>
    public static InjurySeverity SeverityOf(int days)
    {
        if (days <= 7) return InjurySeverity.Leve;
        if (days <= 21) return InjurySeverity.Moderada;
        if (days <= 70) return InjurySeverity.Grave;
        return InjurySeverity.Critica;
    }

    /// <summary>Suma de pesos del catálogo (el sorteo normaliza con ella).</summary>
    public static int TotalWeight => All.Sum(d => d.weight);

    /// <summary>Sorteo ponderado puro. `roll` ∈ [0,1).</summary>
    public static InjuryDef Pick(double roll)
    {
        double r = roll * TotalWeight;
        double cum = 0;
        foreach (var d in All)
        {
            cum += d.weight;
            if (r <= cum) return d;
        }
        return All[0];
    }

    /// <summary>Pronóstico legible ("3-7 días") para el parte médico.</summary>
    public static string Pronostico(InjuryDef d) => d == null ? "—" : $"{d.minDays}-{d.maxDays} días";

    public static string SeverityLabel(InjurySeverity sev) => sev switch
    {
        InjurySeverity.Leve => "LEVE",
        InjurySeverity.Moderada => "MODERADA",
        InjurySeverity.Grave => "GRAVE",
        _ => "CRÍTICA"
    };
}
