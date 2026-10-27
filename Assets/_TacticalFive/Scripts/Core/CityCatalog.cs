using System.Collections.Generic;

/// <summary>
/// Catálogo puro de la capa de ciudad (F7-05): proyectos de infraestructura y
/// eventos con decisiones. Los efectos son deliberadamente pequeños y acotados
/// para no crear una economía paralela a Finanzas/Pabellón/Empleados.
/// </summary>
public static class CityCatalog
{
    public class ProjectDefinition
    {
        public string key;
        public string name;
        public string description;
        public int maxLevel;
        public int days;
        public long baseCost;
        public long costStep;
    }

    public class EventChoice
    {
        public string label;
        public long cost;
        public int fanConfidence;
        public int morale;
        public int fisico;
    }

    public class EventDefinition
    {
        public string key;
        public string title;
        public string description;
        public EventChoice[] choices;
    }

    public const string ProjectArena = "entorno_pabellon";
    public const string ProjectPerformance = "centro_rendimiento";
    public const string ProjectFan = "programa_aficion";
    public const string ProjectMedia = "centro_medios";

    public static readonly ProjectDefinition[] Projects =
    {
        new ProjectDefinition
        {
            key = ProjectArena,
            name = "ENTORNO DEL PABELLÓN",
            description = "Mejora los accesos, la hostelería y la experiencia de la grada. Aumenta la asistencia en los partidos en casa.",
            maxLevel = 2,
            days = 10,
            baseCost = 3_000_000,
            costStep = 3_000_000
        },
        new ProjectDefinition
        {
            key = ProjectPerformance,
            name = "CENTRO DE RENDIMIENTO",
            description = "Instala recuperación avanzada para la plantilla. Mejora el físico que recuperan los jugadores en los días de descanso.",
            maxLevel = 2,
            days = 12,
            baseCost = 4_000_000,
            costStep = 4_000_000
        },
        new ProjectDefinition
        {
            key = ProjectFan,
            name = "PROGRAMA DE AFICIÓN",
            description = "Actividades con abonados, peñas y colegios. Refuerza la confianza de la afición al completarse.",
            maxLevel = 2,
            days = 8,
            baseCost = 2_500_000,
            costStep = 2_500_000
        },
        new ProjectDefinition
        {
            key = ProjectMedia,
            name = "CENTRO DE MEDIOS",
            description = "Sala de prensa y contenidos propios para acercar el club a la ciudad. Mejora la confianza y la moral del vestuario.",
            maxLevel = 1,
            days = 12,
            baseCost = 5_000_000,
            costStep = 0
        }
    };

    public static readonly EventDefinition[] Events =
    {
        new EventDefinition
        {
            key = "queja_vecinal",
            title = "QUEJA VECINAL POR RUIDO",
            description = "Los vecinos del pabellón se han quejado por el ruido de los partidos nocturnos. El ayuntamiento pide una respuesta.",
            choices = new[]
            {
                new EventChoice
                {
                    label = "Insonorizar el pabellón",
                    cost = 500_000,
                    fanConfidence = 6
                },
                new EventChoice
                {
                    label = "Ignorar la queja",
                    cost = 0,
                    fanConfidence = -4
                }
            }
        },
        new EventDefinition
        {
            key = "campana_abonados",
            title = "CAMPAÑA DE ABONADOS",
            description = "La ciudad responde al buen momento del equipo. Una campaña a tiempo puede llenar el pabellón y movilizar a la afición.",
            choices = new[]
            {
                new EventChoice
                {
                    label = "Invertir en la campaña",
                    cost = 1_000_000,
                    fanConfidence = 8
                },
                new EventChoice
                {
                    label = "No invertir",
                    cost = 0,
                    fanConfidence = -2
                }
            }
        },
        new EventDefinition
        {
            key = "visita_escolar",
            title = "VISITA ESCOLAR",
            description = "Varios colegios quieren visitar el pabellón y conocer a la plantilla. Es una oportunidad para crear cantera de aficionados.",
            choices = new[]
            {
                new EventChoice
                {
                    label = "Organizar la visita",
                    cost = 300_000,
                    fanConfidence = 4,
                    morale = 2
                },
                new EventChoice
                {
                    label = "Declinar la invitación",
                    cost = 0,
                    fanConfidence = -3
                }
            }
        },
        new EventDefinition
        {
            key = "ola_calor",
            title = "OLA DE CALOR EN LA CIUDAD",
            description = "Una ola de calor azota la ciudad. El pabellón y las instalaciones de entrenamiento necesitan una decisión.",
            choices = new[]
            {
                new EventChoice
                {
                    label = "Climatizar las zonas de trabajo",
                    cost = 400_000,
                    fisico = 4
                },
                new EventChoice
                {
                    label = "Mantener el plan habitual",
                    cost = 0,
                    fisico = -3
                }
            }
        },
        new EventDefinition
        {
            key = "rueda_prensa",
            title = "RUEDA DE PRENSA ABIERTA",
            description = "Los medios locales piden acceso al vestuario. Abrir las puertas puede mejorar la imagen del club, pero también genera ruido.",
            choices = new[]
            {
                new EventChoice
                {
                    label = "Abrir el vestuario",
                    cost = 0,
                    fanConfidence = 5
                },
                new EventChoice
                {
                    label = "Cerrar filas",
                    cost = 0,
                    fanConfidence = -2,
                    morale = 3
                }
            }
        }
    };

    public static ProjectDefinition Project(string key)
    {
        foreach (var p in Projects)
            if (p.key == key) return p;
        return null;
    }

    public static EventDefinition Event(string key)
    {
        foreach (var e in Events)
            if (e.key == key) return e;
        return null;
    }

    /// <summary>Coste del siguiente nivel (el nivel empieza en 1).</summary>
    public static long ProjectCost(ProjectDefinition definition, int nextLevel)
    {
        if (definition == null) return 0;
        int idx = UnityEngine.Mathf.Max(1, nextLevel) - 1;
        return definition.baseCost + definition.costStep * idx;
    }

    /// <summary>Multiplicador de asistencia del proyecto de entorno (4 % por nivel).</summary>
    public static float AttendanceMultiplier(int level)
        => 1f + UnityEngine.Mathf.Clamp(level, 0, 2) * 0.04f;

    /// <summary>Físico extra en día de descanso por el centro de rendimiento.</summary>
    public static int RecoveryBonus(int level)
        => UnityEngine.Mathf.Clamp(level, 0, 2) * 2;

    public static string ProjectEffectSummary(string key, int level)
    {
        switch (key)
        {
            case ProjectArena:
                return $"+{UnityEngine.Mathf.RoundToInt((AttendanceMultiplier(level) - 1f) * 100)} % de asistencia en casa.";
            case ProjectPerformance:
                return $"+{RecoveryBonus(level)} de físico en días de descanso.";
            case ProjectFan:
                return "Refuerza la confianza de la afición.";
            case ProjectMedia:
                return "Mejora la confianza y la moral del vestuario.";
            default:
                return "";
        }
    }

    public static string EventEffectSummary(EventChoice choice)
    {
        if (choice == null) return "";
        var parts = new List<string>();
        if (choice.cost > 0) parts.Add($"Coste ${choice.cost:N0}");
        if (choice.fanConfidence != 0) parts.Add($"Afición {(choice.fanConfidence > 0 ? "+" : "")}{choice.fanConfidence}");
        if (choice.morale != 0) parts.Add($"Moral {(choice.morale > 0 ? "+" : "")}{choice.morale}");
        if (choice.fisico != 0) parts.Add($"Físico {(choice.fisico > 0 ? "+" : "")}{choice.fisico}");
        return string.Join(" · ", parts);
    }
}
