using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Transición propuesta por un detector: qué hacer con el hilo y con qué datos.</summary>
public class StorylineTransition
{
    public string type;
    public StorylineService.Action action;
    public int intensity;
    public int subjectPlayerId;
    public string title;
    public string summary;
    public string body;
    public string newsType;
    public string resolution;
    public int morale;
    public int chemistry;
    public int fan;
}

/// <summary>Definición de un tipo de hilo: con qué periodicidad puede volver a abrirse
/// y la función que decide sus transiciones (pura, sin DB).</summary>
public class StorylineDef
{
    public string type;
    public int cooldownDays;
    public System.Func<StorylineContext, StorylineDef, StorylineData, List<StorylineTransition>> detect;
}

/// <summary>
/// Catálogo de hilos narrativos. Añadir un tipo nuevo es añadir una definición y su
/// detector puro (que recibe el contexto del día y el hilo abierto de su tipo).
/// </summary>
public static class StorylineCatalog
{
    // ── Umbrales (documentados en Docs/GAMEPLAY.md) ──
    public const int HOT_GAMES = 3;          // partidos seguidos anotando
    public const int HOT_POINTS = 27;        // puntos para considerar "caliente"
    public const int COLD_GAMES = 3;         // partidos seguidos desaparecido
    public const int COLD_POINTS = 10;       // puntos para considerar "seco"
    public const int UNREST_MORALE = 35;     // moral por debajo de la cual hay descontento
    public const int UNREST_MINUTES_PCT = 50; // % de los minutos esperados por debajo del cual hay descontento

    public static readonly List<StorylineDef> All = new()
    {
        new StorylineDef { type = StorylineType.RachaPositiva, cooldownDays = 10, detect = DetectStreak },
        new StorylineDef { type = StorylineType.Bache,         cooldownDays = 10, detect = DetectStreak },
        new StorylineDef { type = StorylineType.JugadorCaliente, cooldownDays = 15, detect = DetectHotHand },
        new StorylineDef { type = StorylineType.Sequia,          cooldownDays = 15, detect = DetectSlump },
        new StorylineDef { type = StorylineType.Descontento,     cooldownDays = 20, detect = DetectUnrest },
    };

    public static StorylineDef Find(string type) => All.FirstOrDefault(d => d.type == type);

    // ── DETECTORES (puros) ──────────────────────────────────

    /// <summary>Rachas de victorias/derrotas del equipo (5, 10, 15...).</summary>
    public static List<StorylineTransition> DetectStreak(StorylineContext ctx, StorylineDef def, StorylineData active)
    {
        var result = new List<StorylineTransition>();
        string current = StorylineService.TypeFor(ctx.Streak);
        int milestone = StorylineService.Milestone(ctx.Streak);
        int abs = Mathf.Abs(ctx.Streak);
        var (morale, chem, fan) = StorylineService.Effects(def.type, milestone);
        bool up = def.type == StorylineType.RachaPositiva;

        if (active == null)
        {
            if (current != def.type) return result;
            result.Add(new StorylineTransition
            {
                type = def.type,
                action = StorylineService.Action.Start,
                intensity = milestone,
                title = up ? $"Racha de {abs} victorias" : $"Bache de {abs} derrotas",
                summary = up
                    ? $"{abs} triunfos consecutivos del {ctx.myTeam.name}"
                    : $"{abs} derrotas consecutivas del {ctx.myTeam.name}",
                body = up
                    ? $"El {ctx.myTeam.name} está en racha: {abs} victorias seguidas. El vestuario respira confianza y la afición se ilusiona con el proyecto."
                    : $"El {ctx.myTeam.name} atraviesa un bache serio: {abs} derrotas consecutivas. La prensa pregunta por el rumbo del proyecto y el vestuario empieza a mostrar signos de tensión.",
                newsType = up ? NewsType.RACHA_VICTORIAS : NewsType.MALA_RACHA,
                morale = morale, chemistry = chem, fan = fan
            });
            return result;
        }

        if (current != def.type)
        {
            result.Add(new StorylineTransition
            {
                type = def.type,
                action = StorylineService.Action.Resolve,
                resolution = StorylineResolution.Broken,
                summary = up ? "La racha se rompió" : "El bache terminó",
                title = up ? "Fin de la racha" : "Fin del bache",
                body = up
                    ? $"Se acabó la racha del {ctx.myTeam.name}. El equipo deja atrás un tramo brillante que deja al aficionado con ganas de más."
                    : $"El {ctx.myTeam.name} respira: se rompe el bache. Una victoria que puede marcar el punto de inflexión.",
                newsType = up ? NewsType.RACHA_VICTORIAS : NewsType.MALA_RACHA
            });
            return result;
        }

        if (milestone > active.intensity)
        {
            result.Add(new StorylineTransition
            {
                type = def.type,
                action = StorylineService.Action.Escalate,
                intensity = milestone,
                title = up ? $"La racha sigue: {abs} victorias" : $"La crisis se agrava: {abs} derrotas",
                summary = up ? $"La racha del {ctx.myTeam.name} llega a {abs}" : $"El {ctx.myTeam.name} encadena {abs} derrotas",
                body = up
                    ? $"La racha del {ctx.myTeam.name} ya es de {abs} victorias. Lo que empezó como un buen momento se ha convertido en una de las historias de la temporada."
                    : $"El {ctx.myTeam.name} suma ya {abs} derrotas seguidas. La situación es crítica y los rumores sobre cambios no dejan de crecer.",
                newsType = up ? NewsType.RACHA_VICTORIAS : NewsType.MALA_RACHA,
                morale = morale, chemistry = chem, fan = fan
            });
        }
        return result;
    }

    /// <summary>Jugador en racha anotadora (3+ partidos de 27 o más puntos).</summary>
    public static List<StorylineTransition> DetectHotHand(StorylineContext ctx, StorylineDef def, StorylineData active)
    {
        var best = FindPointsStreak(ctx, HOT_POINTS, atLeast: true, minGames: HOT_GAMES);
        return PlayerStreakTransitions(ctx, def, active, best, StorylineType.JugadorCaliente, up: true);
    }

    /// <summary>Jugador en sequía (3+ partidos con 10 puntos o menos).</summary>
    public static List<StorylineTransition> DetectSlump(StorylineContext ctx, StorylineDef def, StorylineData active)
    {
        var best = FindPointsStreak(ctx, COLD_POINTS, atLeast: false, minGames: COLD_GAMES);
        return PlayerStreakTransitions(ctx, def, active, best, StorylineType.Sequia, up: false);
    }

    /// <summary>Descontento: moral baja y minutos muy por debajo de lo que pide su rol.</summary>
    public static List<StorylineTransition> DetectUnrest(StorylineContext ctx, StorylineDef def, StorylineData active)
    {
        var result = new List<StorylineTransition>();

        // Si el hilo está abierto y el jugador recuperó la moral (o se fue), se cierra.
        if (active != null)
        {
            var subj = ctx.FindPlayer(active.subject_player_id);
            if (subj == null || subj.morale >= UNREST_MORALE + 15)
            {
                result.Add(new StorylineTransition
                {
                    type = def.type,
                    action = StorylineService.Action.Resolve,
                    resolution = StorylineResolution.Completed,
                    summary = subj != null ? $"{subj.first_name} {subj.last_name} recuperó la calma" : "El jugador ya no está en el equipo",
                    title = "Vuelve la calma al vestuario",
                    body = $"{active.title} se desactiva: la situación en el vestuario del {ctx.myTeam.name} se ha normalizado.",
                    newsType = NewsType.PREOCUPACION
                });
                return result;
            }

            int bucket = subj.morale < 20 ? 2 : 1;
            if (bucket > active.intensity)
            {
                result.Add(new StorylineTransition
                {
                    type = def.type,
                    action = StorylineService.Action.Escalate,
                    intensity = bucket,
                    subjectPlayerId = subj.id,
                    title = $"Crisis abierta: {subj.first_name} {subj.last_name}",
                    summary = $"{subj.first_name} {subj.last_name} sigue sin minutos y explota",
                    body = $"La situación de {subj.first_name} {subj.last_name} en el {ctx.myTeam.name} es insostenible: su entorno deja entrever una petición de salida si no cambia su rol.",
                    newsType = NewsType.QUEJA,
                    chemistry = -1, fan = -1
                });
            }
            return result;
        }

        var unhappy = FindUnderusedPlayer(ctx);
        if (unhappy == null) return result;

        var p = unhappy.Value.player;
        result.Add(new StorylineTransition
        {
            type = def.type,
            action = StorylineService.Action.Start,
            intensity = 1,
            subjectPlayerId = p.id,
            title = $"Descontento: {p.first_name} {p.last_name}",
            summary = $"{p.first_name} {p.last_name} juega muy por debajo de lo que pide su rol",
            body = $"{p.first_name} {p.last_name} no está conforme con los minutos que está jugando en el {ctx.myTeam.name}. " +
                   $"Su rol prometido era otro y el vestuario empieza a notar el malestar.\n\nLa dirección deportiva deberá decidir si le da más protagonismo o busca una salida.",
            newsType = NewsType.PREOCUPACION,
            chemistry = -1
        });
        return result;
    }

    // ── APOYO PURO ──────────────────────────────────────────

    /// <summary>Minutos que el rol del jugador hace esperar (espejo de la heurística del simulador).</summary>
    public static int ExpectedMinutes(PlayerRole role) => role switch
    {
        PlayerRole.Estrella => 39,
        PlayerRole.Titular => 30,
        PlayerRole.Banquillo => 16,
        _ => 8
    };

    /// <summary>Jugador con más partidos seguidos cumpliendo el umbral de puntos.</summary>
    public static (PlayerData player, int games, float avg)? FindPointsStreak(StorylineContext ctx, int points,
                                                                             bool atLeast, int minGames)
    {
        (PlayerData, int, float)? best = null;
        foreach (var p in ctx.Roster)
        {
            if (!ctx.RecentByPlayer.TryGetValue(p.id, out var games) || games.Count < minGames) continue;

            int streak = 0;
            float sum = 0f;
            foreach (var g in games)
            {
                bool ok = atLeast ? g.points >= points : g.points <= points;
                if (!ok) break;
                streak++;
                sum += g.points;
            }
            if (streak < minGames) continue;

            float avg = sum / streak;
            if (best == null || streak > best.Value.Item2
                || (streak == best.Value.Item2 && avg > best.Value.Item3))
                best = (p, streak, avg);
        }
        return best;
    }

    /// <summary>Jugador con moral baja que juega por debajo de la mitad de lo que pide su rol.</summary>
    public static (PlayerData player, int avgMinutes)? FindUnderusedPlayer(StorylineContext ctx)
    {
        (PlayerData, int)? worst = null;
        foreach (var p in ctx.Roster)
        {
            if (p.morale >= UNREST_MORALE || p.injury_days > 0) continue;
            if (p.role == PlayerRole.UltimoRecurso) continue; // su rol ya asume poco protagonismo
            if (!ctx.RecentByPlayer.TryGetValue(p.id, out var games) || games.Count < 3) continue;

            int expected = p.rotation_minutes >= 0 ? p.rotation_minutes : ExpectedMinutes(p.role);
            if (expected <= 0) continue;
            float avgMinutes = games.Average(g => g.minutes);
            if (avgMinutes * 100f / expected > UNREST_MINUTES_PCT) continue;

            if (worst == null || p.morale < worst.Value.Item1.morale)
                worst = (p, Mathf.RoundToInt(avgMinutes));
        }
        return worst;
    }

    static List<StorylineTransition> PlayerStreakTransitions(StorylineContext ctx, StorylineDef def, StorylineData active,
        (PlayerData player, int games, float avg)? best, string type, bool up)
    {
        var result = new List<StorylineTransition>();

        if (active == null)
        {
            if (best == null) return result;
            var (p, streak, avg) = best.Value;
            result.Add(new StorylineTransition
            {
                type = type,
                action = StorylineService.Action.Start,
                intensity = 1,
                subjectPlayerId = p.id,
                title = up ? $"En racha: {p.first_name} {p.last_name}" : $"Sequía: {p.first_name} {p.last_name}",
                summary = up
                    ? $"{streak} partidos seguidos por encima de {HOT_POINTS} puntos ({avg:N1} de media)"
                    : $"{streak} partidos seguidos por debajo de {COLD_POINTS} puntos ({avg:N1} de media)",
                body = up
                    ? $"{p.first_name} {p.last_name} está en un momento dulce: {streak} partidos consecutivos superando los {HOT_POINTS} puntos, con {avg:N1} de media.\n\nSu nombre empieza a sonar en las quinielas de los premios de la temporada."
                    : $"{p.first_name} {p.last_name} no encuentra su sitio: {streak} partidos seguidos por debajo de los {COLD_POINTS} puntos.\n\nEl vestuario y la prensa se preguntan qué le está pasando.",
                newsType = NewsType.EXPLOSION,
                morale = up ? 3 : -2
            });
            return result;
        }

        // El hilo está abierto: si el jugador ya no está o dejó de cumplir la racha, se cierra.
        var subject = ctx.FindPlayer(active.subject_player_id);
        bool stillOn = subject != null &&
                       best != null && best.Value.Item1.id == subject.id;
        if (!stillOn)
        {
            result.Add(new StorylineTransition
            {
                type = type,
                action = StorylineService.Action.Resolve,
                resolution = StorylineResolution.Broken,
                summary = up ? "El jugador se enfrió" : "El jugador rompió su mala racha",
                title = up ? "Se acabó el momento dulce" : "Fin de la sequía",
                body = up
                    ? $"{active.title} llega a su fin: el jugador ya no mantiene el nivel de las últimas semanas."
                    : $"{active.title} llega a su fin: el jugador ha firmado un partido que rompe la mala dinámica.",
                newsType = up ? NewsType.EXPLOSION : NewsType.SEQUIA
            });
            return result;
        }

        int milestone = 1 + (best.Value.Item2 - (up ? HOT_GAMES : COLD_GAMES)) / 2;
        if (milestone > active.intensity)
        {
            result.Add(new StorylineTransition
            {
                type = type,
                action = StorylineService.Action.Escalate,
                intensity = milestone,
                subjectPlayerId = subject.id,
                title = up ? $"{subject.first_name} {subject.last_name} sigue imparable" : $"La sequía de {subject.first_name} continúa",
                summary = up
                    ? $"{best.Value.Item2} partidos seguidos al máximo nivel"
                    : $"{best.Value.Item2} partidos seguidos sin aparecer",
                body = up
                    ? $"{subject.first_name} {subject.last_name} encadena {best.Value.Item2} partidos por encima de los {HOT_POINTS} puntos. La liga entera habla de su momento de forma."
                    : $"{subject.first_name} {subject.last_name} acumula {best.Value.Item2} partidos por debajo de los {COLD_POINTS} puntos. Empieza a ser un problema serio para el equipo.",
                newsType = up ? NewsType.EXPLOSION : NewsType.SEQUIA,
                morale = up ? 2 : -2
            });
        }
        return result;
    }
}
