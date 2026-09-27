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

/// <summary>
/// Hilos activos de un tipo, indexables por protagonista. Permite que un tipo "por
/// jugador" (sequía, racha anotadora) tenga varios hilos a la vez, uno por jugador.
/// </summary>
public class StorylineSubjects
{
    public List<StorylineData> Actives = new();
    /// <summary>Hilo abierto del protagonista indicado (o null; 0 = hilo de equipo).</summary>
    public StorylineData For(int playerId) => Actives.FirstOrDefault(a => a.subject_player_id == playerId);
}

/// <summary>Definición de un tipo de hilo: con qué periodicidad puede volver a abrirse
/// y la función que decide sus transiciones (pura, sin DB).</summary>
public class StorylineDef
{
    public string type;
    public int cooldownDays;
    public System.Func<StorylineContext, StorylineDef, StorylineData, List<StorylineTransition>> detect;
    /// <summary>
    /// Hilos POR JUGADOR: si está definido, el tipo admite varios hilos simultáneos (uno
    /// por protagonista) y este detector los gobierna todos. El servicio lo prefiere sobre `detect`.
    /// </summary>
    public System.Func<StorylineContext, StorylineDef, StorylineSubjects, List<StorylineTransition>> detectPlayers;
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
    public const int COLD_POINTS_MIN = 3;    // suelo del umbral de sequía (un 5 ppg necesita ≤3, no ≤6)
    public const float COLD_PCT = 0.45f;     // sequía = ≤45% de su media anotadora de la temporada
    public const float SLUMP_MIN_AVG_MINUTES = 12f; // minutos medios para que una sequía sea creíble
    public const int UNREST_MORALE = 35;     // moral por debajo de la cual hay descontento
    public const int UNREST_MINUTES_PCT = 50; // % de los minutos esperados por debajo del cual hay descontento
    public const int STAR_OVERALL = 85;       // overall a partir del cual una baja es "de estrella"
    public const int STAR_INJURY_DAYS = 7;    // días de baja para abrir el hilo
    public const int GM_FAN_LOW = 30;         // afición por debajo de la cual hay presión
    public const int GM_TRUST_LOW = 45;       // confianza del dueño por debajo de la cual hay presión
    public const int RACE_GAMES_LEFT = 25;    // partidos restantes para abrir la pelea por playoffs
    public const int MAX_PLAYER_HILOS = 2;    // hilos simultáneos por tipo de jugador (uno por protagonista)

    public static readonly List<StorylineDef> All = new()
    {
        new StorylineDef { type = StorylineType.BajaEstrella,    cooldownDays = 10, detect = DetectStarInjury },
        new StorylineDef { type = StorylineType.Bache,           cooldownDays = 10, detect = DetectStreak },
        new StorylineDef { type = StorylineType.RachaPositiva,   cooldownDays = 10, detect = DetectStreak },
        new StorylineDef { type = StorylineType.Descontento,     cooldownDays = 20, detect = DetectUnrest },
        new StorylineDef { type = StorylineType.RumorTraspaso,   cooldownDays = 20, detect = DetectTradeRumor },
        new StorylineDef { type = StorylineType.PresionGM,       cooldownDays = 25, detect = DetectGmPressure },
        new StorylineDef { type = StorylineType.CazaPlayoffs,    cooldownDays = 20, detect = DetectPlayoffRace },
        new StorylineDef { type = StorylineType.JugadorCaliente, cooldownDays = 15, detect = DetectHotHand, detectPlayers = DetectHotHandPlayers },
        new StorylineDef { type = StorylineType.Sequia,          cooldownDays = 15, detect = DetectSlump,   detectPlayers = DetectSlumpPlayers },
        new StorylineDef { type = StorylineType.CrisisEconomica, cooldownDays = 25, detect = DetectEconomicCrisis },
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

    /// <summary>Jugador en sequía: 3+ partidos por debajo de SU umbral (relativo a su media).</summary>
    public static List<StorylineTransition> DetectSlump(StorylineContext ctx, StorylineDef def, StorylineData active)
    {
        var best = FindPointsStreak(ctx, COLD_POINTS_MIN, atLeast: false, minGames: COLD_GAMES,
                                    minAvgMinutes: SLUMP_MIN_AVG_MINUTES);
        return PlayerStreakTransitions(ctx, def, active, best, StorylineType.Sequia, up: false);
    }

    /// <summary>
    /// Umbral de "partido seco" de un jugador. Relativo a su media anotadora de la
    /// temporada (≤45%) con un suelo absoluto: un especialista defensivo de 5 puntos por
    /// partido no está "en sequía" cada noche, y una estrella de 30 sí lo está al anotar 14.
    /// </summary>
    public static int ColdThreshold(StorylineContext ctx, PlayerData p)
    {
        float ppg = 0f;
        if (ctx != null && ctx.SeasonPpg != null) ctx.SeasonPpg.TryGetValue(p.id, out ppg);
        return Mathf.Max(COLD_POINTS_MIN, Mathf.RoundToInt(ppg * COLD_PCT));
    }

    /// <summary>Jugadores en racha anotadora: hasta `MAX_PLAYER_HILOS` a la vez, uno por jugador.</summary>
    public static List<StorylineTransition> DetectHotHandPlayers(StorylineContext ctx, StorylineDef def, StorylineSubjects subjects)
    {
        var candidates = FindPointsStreakCandidates(ctx, HOT_POINTS, atLeast: true, minGames: HOT_GAMES);
        return PlayerStreakTransitionsMulti(ctx, def, subjects, candidates, StorylineType.JugadorCaliente, up: true);
    }

    /// <summary>Jugadores en sequía: hasta `MAX_PLAYER_HILOS` a la vez, uno por jugador.</summary>
    public static List<StorylineTransition> DetectSlumpPlayers(StorylineContext ctx, StorylineDef def, StorylineSubjects subjects)
    {
        var candidates = FindPointsStreakCandidates(ctx, COLD_POINTS_MIN, atLeast: false, minGames: COLD_GAMES,
                                                    minAvgMinutes: SLUMP_MIN_AVG_MINUTES);
        return PlayerStreakTransitionsMulti(ctx, def, subjects, candidates, StorylineType.Sequia, up: false);
    }

    /// <summary>
    /// Transiciones de un tipo POR JUGADOR: abre/escala el hilo de cada candidato y cierra
    /// el de los protagonistas que ya no cumplen. Varios jugadores pueden estar en sequía
    /// (o en racha) a la vez, cada uno con su propio hilo.
    /// </summary>
    static List<StorylineTransition> PlayerStreakTransitionsMulti(StorylineContext ctx, StorylineDef def,
        StorylineSubjects subjects, List<(PlayerData player, int streak, float avg)> candidates,
        string type, bool up)
    {
        var result = new List<StorylineTransition>();

        foreach (var cand in candidates)
            result.AddRange(PlayerStreakTransitions(ctx, def, subjects.For(cand.player.id), cand, type, up));

        // Protagonistas con hilo abierto que ya no cumplen la racha: se cierran.
        foreach (var active in subjects.Actives)
        {
            if (active.subject_player_id <= 0) continue;
            if (candidates.Any(c => c.player.id == active.subject_player_id)) continue;
            result.Add(ResolvePlayerStreak(active, up));
        }

        return result;
    }

    /// <summary>Cierre de un hilo de jugador que ya no cumple la racha.</summary>
    static StorylineTransition ResolvePlayerStreak(StorylineData active, bool up)
    {
        return new StorylineTransition
        {
            type = active.type,
            action = StorylineService.Action.Resolve,
            resolution = StorylineResolution.Broken,
            subjectPlayerId = active.subject_player_id,
            summary = up ? "El jugador se enfrió" : "El jugador rompió su mala racha",
            title = up ? "Se acabó el momento dulce" : "Fin de la sequía",
            body = up
                ? $"{active.title} llega a su fin: el jugador ya no mantiene el nivel de las últimas semanas."
                : $"{active.title} llega a su fin: el jugador ha firmado un partido que rompe la mala dinámica.",
            newsType = up ? NewsType.EXPLOSION : NewsType.SEQUIA
        };
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
                    newsType = NewsType.DESCONTENTO
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
                    newsType = NewsType.DESCONTENTO,
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
            newsType = NewsType.DESCONTENTO,
            chemistry = -1
        });
        return result;
    }

    /// <summary>Jugador en el mercado (trade block o expirante descontento).</summary>
    public static List<StorylineTransition> DetectTradeRumor(StorylineContext ctx, StorylineDef def, StorylineData active)
    {
        var result = new List<StorylineTransition>();

        PlayerData candidate = ctx.Roster
            .Where(p => p.injury_days == 0)
            .OrderByDescending(p => (p.on_trade_block == 1 ? 100 : 0) + p.overall)
            .FirstOrDefault(p => p.on_trade_block == 1 || (p.contract_years <= 1 && p.morale < 40));

        if (active != null)
        {
            var subj = ctx.FindPlayer(active.subject_player_id);
            bool stillRumor = subj != null && (subj.on_trade_block == 1 || (subj.contract_years <= 1 && subj.morale < 40));
            if (subj == null)
            {
                result.Add(new StorylineTransition
                {
                    type = def.type, action = StorylineService.Action.Resolve,
                    resolution = StorylineResolution.Completed,
                    summary = "El jugador salió del equipo",
                    title = "Se resolvió el culebrón",
                    body = $"{active.title} termina: el jugador ya no está en el {ctx.myTeam.name} y la operación se ha cerrado.",
                    newsType = NewsType.RUMOR_TRASPASO
                });
            }
            else if (!stillRumor)
            {
                result.Add(new StorylineTransition
                {
                    type = def.type, action = StorylineService.Action.Resolve,
                    title = "Vuelve la calma",
                    summary = $"{subj.first_name} {subj.last_name} ya no está en el mercado",
                    body = $"{subj.first_name} {subj.last_name} deja de estar en el mercado y su situación en el {ctx.myTeam.name} se normaliza.",
                    newsType = NewsType.RUMOR_TRASPASO
                });
            }
            else if (ctx.gameDay - active.last_update_game_day >= 7)
            {
                result.Add(new StorylineTransition
                {
                    type = def.type, action = StorylineService.Action.Escalate,
                    intensity = active.intensity + 1,
                    subjectPlayerId = subj.id,
                    title = $"Culebrón: {subj.first_name} {subj.last_name}",
                    summary = "Los rumores se intensifican y la operación podría cerrarse en cualquier momento",
                    body = $"El futuro de {subj.first_name} {subj.last_name} sigue sin resolverse y los rumores se intensifican. Varias franquicias han preguntado por su situación y el {ctx.myTeam.name} estudia ofertas.",
                    newsType = NewsType.RUMOR_TRASPASO, chemistry = -1
                });
            }
            return result;
        }

        if (candidate == null) return result;

        result.Add(new StorylineTransition
        {
            type = def.type, action = StorylineService.Action.Start, intensity = 1,
            subjectPlayerId = candidate.id,
            title = $"Rumores: {candidate.first_name} {candidate.last_name}",
            summary = candidate.on_trade_block == 1
                ? $"{candidate.first_name} {candidate.last_name} está en el mercado"
                : $"El contrato de {candidate.first_name} {candidate.last_name} expira y no hay acuerdo",
            body = $"{candidate.first_name} {candidate.last_name} vive un momento de incertidumbre en el {ctx.myTeam.name}: " +
                   "su nombre suena en los despachos y varias franquicias han preguntado por él.\n\nEl club deberá decidir si lo retiene o busca una operación.",
            newsType = NewsType.RUMOR_TRASPASO, morale = -1, chemistry = -1
        });
        return result;
    }

    /// <summary>Baja de una estrella (lesión larga).</summary>
    public static List<StorylineTransition> DetectStarInjury(StorylineContext ctx, StorylineDef def, StorylineData active)
    {
        var result = new List<StorylineTransition>();

        if (active != null)
        {
            var subj = ctx.FindPlayer(active.subject_player_id);
            if (subj == null) return result;

            if (subj.injury_days <= 0)
            {
                result.Add(new StorylineTransition
                {
                    type = def.type, action = StorylineService.Action.Resolve,
                    resolution = StorylineResolution.Completed,
                    summary = $"{subj.first_name} {subj.last_name} vuelve a las canchas",
                    title = $"Vuelve {subj.first_name} {subj.last_name}",
                    body = $"{subj.first_name} {subj.last_name} está de vuelta después de su lesión. La afición del {ctx.myTeam.name} recupera la ilusión.",
                    newsType = NewsType.BAJA_ESTRELLA, morale = 2, fan = 2
                });
            }
            else if (subj.injury_days >= 21 && active.intensity < 2)
            {
                result.Add(new StorylineTransition
                {
                    type = def.type, action = StorylineService.Action.Escalate, intensity = 2,
                    subjectPlayerId = subj.id,
                    title = $"Sin {subj.first_name} {subj.last_name}: la lesión se alarga",
                    summary = $"La baja se alarga y el equipo nota su ausencia",
                    body = $"La lesión de {subj.first_name} {subj.last_name} se alarga más de lo previsto y el {ctx.myTeam.name} tendrá que sobrevivir sin él durante más semanas.",
                    newsType = NewsType.BAJA_ESTRELLA, fan = -2, chemistry = -1
                });
            }
            return result;
        }

        var star = ctx.Roster
            .Where(p => p.overall >= STAR_OVERALL && p.injury_days >= STAR_INJURY_DAYS)
            .OrderByDescending(p => p.overall)
            .FirstOrDefault();
        if (star == null) return result;

        result.Add(new StorylineTransition
        {
            type = def.type, action = StorylineService.Action.Start, intensity = 1,
            subjectPlayerId = star.id,
            title = $"Baja sensible: {star.first_name} {star.last_name}",
            summary = $"{star.first_name} {star.last_name} estará fuera al menos {star.injury_days} días",
            body = $"El {ctx.myTeam.name} pierde temporalmente a {star.first_name} {star.last_name}, uno de sus jugadores más importantes. " +
                   $"La enfermería confirma que estará fuera al menos {star.injury_days} días.\n\nEl equipo tendrá que reordenar su rotación para tapar su ausencia.",
            newsType = NewsType.BAJA_ESTRELLA, fan = -2, chemistry = -1
        });
        return result;
    }

    /// <summary>Presión sobre el GM: afición y confianza del dueño bajo mínimos.</summary>
    public static List<StorylineTransition> DetectGmPressure(StorylineContext ctx, StorylineDef def, StorylineData active)
    {
        var result = new List<StorylineTransition>();
        var m = ctx.manager;
        int fan = m.fan_confidence;
        int trust = m.trust;
        bool pressure = fan < GM_FAN_LOW && trust < GM_TRUST_LOW;

        if (active != null)
        {
            if (!pressure && fan >= GM_FAN_LOW + 15)
            {
                result.Add(new StorylineTransition
                {
                    type = def.type, action = StorylineService.Action.Resolve,
                    resolution = StorylineResolution.Completed,
                    title = "La afición vuelve a creer",
                    summary = "La presión sobre la dirección se disipa",
                    body = $"La situación en el {ctx.myTeam.name} se relaja: los resultados y el apoyo de la grada devuelven la calma a la dirección deportiva.",
                    newsType = NewsType.HITO, fan = 2
                });
                return result;
            }
            if (pressure && fan < GM_FAN_LOW - 10 && active.intensity < 2)
            {
                result.Add(new StorylineTransition
                {
                    type = def.type, action = StorylineService.Action.Escalate, intensity = 2,
                    title = "Crisis institucional",
                    summary = "La grada pide responsabilidades",
                    body = $"La situación en el {ctx.myTeam.name} es crítica: la afición pide explicaciones y la continuidad de la dirección deportiva empieza a cuestionarse seriamente.",
                    newsType = NewsType.QUEJA, fan = -1, chemistry = -1
                });
            }
            return result;
        }

        if (!pressure) return result;

        result.Add(new StorylineTransition
        {
            type = def.type, action = StorylineService.Action.Start, intensity = 1,
            title = "Presión sobre la dirección deportiva",
            summary = "La afición y el dueño empiezan a perder la paciencia",
            body = $"El ambiente en el {ctx.myTeam.name} se ha enrarecido. La afición muestra su malestar con abucheos y desde el consejo llegan mensajes poco tranquilizadores sobre la gestión deportiva.",
            newsType = NewsType.PREOCUPACION, fan = -1
        });
        return result;
    }

    /// <summary>Pelea por el Play-In o el playoff directo (o riesgo de lottery) al final de temporada.</summary>
    public static List<StorylineTransition> DetectPlayoffRace(StorylineContext ctx, StorylineDef def, StorylineData active)
    {
        var result = new List<StorylineTransition>();
        int rank = ctx.ConferenceRank;
        if (rank <= 0) return result;

        bool inRace = ctx.GamesRemaining <= RACE_GAMES_LEFT && rank >= 7;
        int bucket = rank >= 13 ? 2 : rank >= 7 ? 1 : 0;

        if (active != null)
        {
            if (rank <= 6)
            {
                result.Add(new StorylineTransition
                {
                    type = def.type, action = StorylineService.Action.Resolve,
                    resolution = StorylineResolution.Completed,
                    title = "Objetivo cumplido: playoff directo",
                    summary = $"El {ctx.myTeam.name} asalta el playoff directo",
                    body = $"El {ctx.myTeam.name} cierra la temporada regular en posición de playoff directo. La apuesta de la dirección ha funcionado.",
                    newsType = NewsType.HITO, fan = 3
                });
                return result;
            }
            if (bucket > active.intensity)
            {
                result.Add(new StorylineTransition
                {
                    type = def.type, action = StorylineService.Action.Escalate, intensity = bucket,
                    title = "Riesgo de lottery",
                    summary = "El equipo se cae de la pelea",
                    body = $"Al {ctx.myTeam.name} se le complica incluso el Play-In: el equipo cae en zona de lottery y la temporada amenaza con quedar en nada.",
                    newsType = NewsType.MALA_RACHA, fan = -2
                });
            }
            return result;
        }

        if (!inRace) return result;

        result.Add(new StorylineTransition
        {
            type = def.type, action = StorylineService.Action.Start, intensity = bucket,
            title = bucket >= 2 ? "Riesgo de lottery" : "Pelea por el Play-In",
            summary = bucket >= 2
                ? $"{ctx.GamesRemaining} partidos y el equipo fuera de todo"
                : $"Quedan {ctx.GamesRemaining} partidos y el Play-In está en juego",
            body = bucket >= 2
                ? $"Al {ctx.myTeam.name} se le escapa la temporada: con {ctx.GamesRemaining} partidos por jugar, el equipo está en zona de lottery y necesita una reacción urgente."
                : $"Recta final apretada: al {ctx.myTeam.name} le quedan {ctx.GamesRemaining} partidos para meterse en el Play-In. Cada partido vale doble.",
            newsType = NewsType.HITO, fan = bucket >= 2 ? -1 : 1
        });
        return result;
    }

    /// <summary>Crisis económica: presupuesto en rojo o demasiados avisos.</summary>
    public static List<StorylineTransition> DetectEconomicCrisis(StorylineContext ctx, StorylineDef def, StorylineData active)
    {
        var result = new List<StorylineTransition>();
        long budget = ctx.myTeam.budget;
        int warnings = ctx.manager.budget_red_warnings;
        bool crisis = budget < 0 || warnings >= 2;

        if (active != null)
        {
            if (!crisis)
            {
                result.Add(new StorylineTransition
                {
                    type = def.type, action = StorylineService.Action.Resolve,
                    resolution = StorylineResolution.Completed,
                    title = "Las cuentas se equilibran",
                    summary = "El club sale de números rojos",
                    body = $"Las cuentas del {ctx.myTeam.name} vuelven a estar en orden y la preocupación por el presupuesto desaparece.",
                    newsType = NewsType.NOMINA, fan = 1
                });
                return result;
            }
            if (budget < -10_000_000 && active.intensity < 2)
            {
                result.Add(new StorylineTransition
                {
                    type = def.type, action = StorylineService.Action.Escalate, intensity = 2,
                    title = "Alarma financiera",
                    summary = "El déficit se dispara",
                    body = $"El déficit del {ctx.myTeam.name} se agrava y en el club empiezan a plantearse recortes severos si no llegan ingresos.",
                    newsType = NewsType.NOMINA, chemistry = -1
                });
            }
            return result;
        }

        if (!crisis) return result;

        result.Add(new StorylineTransition
        {
            type = def.type, action = StorylineService.Action.Start, intensity = 1,
            title = "Preocupación en las cuentas",
            summary = "El club entra en números rojos",
            body = $"La situación económica del {ctx.myTeam.name} empieza a ser preocupante: el presupuesto está en rojo y desde el consejo llegan peticiones de ajuste.",
            newsType = NewsType.NOMINA, fan = -1
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
                                                                             bool atLeast, int minGames,
                                                                             float minAvgMinutes = 0f)
    {
        var best = FindPointsStreakCandidates(ctx, points, atLeast, minGames, minAvgMinutes, maxCandidates: 1);
        if (best.Count == 0) return null;
        var c = best[0];
        return (c.player, c.streak, c.avg);
    }

    /// <summary>
    /// Todos los jugadores que cumplen la racha, ordenados por relevancia (racha más larga,
    /// luego mayor OVR y luego mejor media) y capados a `maxCandidates`. Base de los hilos
    /// por jugador: permiten narrar a la vez a los dos protagonistas más relevantes.
    /// </summary>
    public static List<(PlayerData player, int streak, float avg)> FindPointsStreakCandidates(
        StorylineContext ctx, int points, bool atLeast, int minGames,
        float minAvgMinutes = 0f, int maxCandidates = StorylineCatalog.MAX_PLAYER_HILOS)
    {
        var found = new List<(PlayerData, int, float)>();
        foreach (var p in ctx.Roster)
        {
            // Una sequía debe ser creíble: no se narra la de quien apenas juega ni la de
            // un último recurso (su poco protagonismo ya viene del rol, ver Descontento).
            if (minAvgMinutes > 0f && p.role == PlayerRole.UltimoRecurso) continue;
            if (!ctx.RecentByPlayer.TryGetValue(p.id, out var games) || games.Count < minGames) continue;
            if (minAvgMinutes > 0f && games.Average(g => g.minutes) < minAvgMinutes) continue;

            // El umbral es relativo a su media cuando el detector lo pide (sequía).
            int threshold = atLeast ? points : Mathf.Max(points, ColdThreshold(ctx, p));

            int streak = 0;
            float sum = 0f;
            foreach (var g in games)
            {
                bool ok = atLeast ? g.points >= threshold : g.points <= threshold;
                if (!ok) break;
                streak++;
                sum += g.points;
            }
            if (streak < minGames) continue;
            found.Add((p, streak, sum / streak));
        }

        return found
            .OrderByDescending(c => c.Item2)                // racha más larga
            .ThenByDescending(c => c.Item1.overall)         // jugador más relevante
            .ThenByDescending(c => c.Item3)                 // y mejor media
            .Take(maxCandidates)
            .ToList();
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
            int cold = ColdThreshold(ctx, p);
            result.Add(new StorylineTransition
            {
                type = type,
                action = StorylineService.Action.Start,
                intensity = 1,
                subjectPlayerId = p.id,
                title = up ? $"En racha: {p.first_name} {p.last_name}" : $"Sequía: {p.first_name} {p.last_name}",
                summary = up
                    ? $"{streak} partidos seguidos por encima de {HOT_POINTS} puntos ({avg:N1} de media)"
                    : $"{streak} partidos seguidos por debajo de {cold} puntos ({avg:N1} de media)",
                body = up
                    ? $"{p.first_name} {p.last_name} está en un momento dulce: {streak} partidos consecutivos superando los {HOT_POINTS} puntos, con {avg:N1} de media.\n\nSu nombre empieza a sonar en las quinielas de los premios de la temporada."
                    : $"{p.first_name} {p.last_name} no encuentra su sitio: {streak} partidos seguidos por debajo de los {cold} puntos.\n\nEl vestuario y la prensa se preguntan qué le está pasando.",
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
            result.Add(ResolvePlayerStreak(active, up));
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
                    : $"{subject.first_name} {subject.last_name} acumula {best.Value.Item2} partidos por debajo de los {ColdThreshold(ctx, subject)} puntos. Empieza a ser un problema serio para el equipo.",
                newsType = up ? NewsType.EXPLOSION : NewsType.SEQUIA,
                morale = up ? 2 : -2
            });
        }
        return result;
    }
}
