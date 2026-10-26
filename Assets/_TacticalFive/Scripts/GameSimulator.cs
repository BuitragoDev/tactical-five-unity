using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

public static class GameSimulator
{
    // F3-04: parámetros de calibración contra NBA 2024-25. Mantenerlos como fuentes
    // únicas para que el perfil Monte Carlo y las fórmulas no se desincronicen.
    public const float BASE_PACE = 96f;
    public const float BASE_TURNOVER_CHANCE = 0.147f;
    const float BASE_REACH_IN_FOUL_CHANCE = 0.125f;
    const float BASE_THREE_POINT_PERCENT = 0.400f;
    const float THREE_POINT_ATTRIBUTE_SLOPE = 0.0035f;
    const float BASE_TWO_POINT_PERCENT = 0.5165f;
    const float TWO_POINT_ATTRIBUTE_SLOPE = 0.0025f;
    const float BASE_ASSISTED_FIELD_GOAL_CHANCE = 0.63f;
    const float OFFENSIVE_REBOUND_WEIGHT_SCALE = 0.42f;
    // F3-05: fase final de los partidos abiertos. Un cuarto se juega un número fijo de
    // posesiones, así que el reloj no se puede alargar por sí solo: si al cerrar el 4.º
    // cuarto el partido sigue abierto (1-6 puntos), se añaden hasta tres posesiones
    // alternando al equipo por detrás y al líder (juego de faltas / última posesión).
    public const int LATE_EXTENSION_MAX_POSS = 3;
    public const int LATE_EXTENSION_MAX_MARGIN = 6;

    public class PlayerStatSnapshot
    {
        public int player_id;
        public string name;
        public string position;
        public string secondary_position;
        public PlayerRole role;
        public bool starter;
        public int target_minutes;
        // Minutos fijados por el jugador en la pantalla ROTACIÓN (no heurística):
        // solo la plantilla del usuario los trae, y el reparto los respeta de verdad.
        public bool fixed_minutes;
        // F2-11: el partido tiene un plan de minutos (fijado por el usuario o calculado
        // por la rotación estratégica automática). El simulador lo respeta con las
        // ventanas densas de cambio, también para la IA.
        public bool planned;
        public int overall, shooting, three_point, passing, rebounding, defense, steals_attr, blocks_attr;
        // Atributos con efecto propio en el motor (F2-04): velocidad = ritmo,
        // bote = pérdidas, atletismo = finalización y rebote, IQ = selección de tiro.
        public int speed, dribbling, athleticism, iq;
        public float minutes;
        public int fgm, fga, fg3m, fg3a, ftm, fta;
        public int oreb, dreb;
        public int assists, steals, blocks, turnovers, pf, points, rating;
        public int double_double, triple_double;
        public int fisico;
    }

    public class TeamStats
    {
        public int fgm, fga, fg3m, fg3a, ftm, fta, points;
        public int oreb, dreb, reb;
        public int assists, steals, blocks, turnovers, pf;
    }

    public class PlayByPlayEvent
    {
        public int quarter;
        public string text;
        public int homeScore;
        public int awayScore;
        public float timeElapsed;
        public List<StatDelta> deltas;
    }

    public class StatDelta
    {
        public int player_id;
        public string stat;
        public float amount;
    }

    public class PossessionOutcome
    {
        public int pts;
        public string desc;
        /// <summary>Rebote ofensivo: el mismo equipo sigue atacando (segunda oportunidad).
        /// Canasta, pérdida, tapón o rebote defensivo lo dejan en false (cambia el balón).</summary>
        public bool offensiveRebound;
    }

    /// <summary>
    /// Contadores de posesiones de un partido. `possessions` cuenta las posesiones
    /// (la unidad que alterna entre equipos) y `attacks` los ataques ejecutados: un
    /// rebote ofensivo añade ataques DENTRO de la misma posesión, así que
    /// `attacks >= possessions` siempre y la diferencia son las segundas oportunidades.
    /// Diagnóstico (F2-01), no se persiste.
    /// </summary>
    public class PossessionTally
    {
        public int homePossessions, awayPossessions;
        public int homeAttacks, awayAttacks;
        /// <summary>Posesiones añadidas por la fase final del 4.º cuarto (F3-05):
        /// 0 = el partido se cerró en posesiones regulares.</summary>
        public int lateExtensionPossessions;

        public int HomeSecondChanceAttacks => Math.Max(0, homeAttacks - homePossessions);
        public int AwaySecondChanceAttacks => Math.Max(0, awayAttacks - awayPossessions);
    }

    /// <summary>
    /// Estado explícito de la posesión (F2-02). Sustituye a la moneda: el atacante
    /// sale de una regla y no del azar.
    /// - Al empezar cada periodo saca el equipo que no sacó en el anterior
    ///   (Q1 local, Q2 visitante, Q3 local, Q4 visitante, prórrogas alternando).
    /// - Dentro del periodo, el balón cambia al terminar cada posesión (canasta,
    ///   pérdida, tapón o rebote defensivo). El rebote ofensivo no cambia de dueño:
    ///   lo resuelve `RunPossessionChain` dentro de la misma posesión.
    /// </summary>
    public class PossessionState
    {
        public bool HomeOnOffense { get; private set; }
        public PossessionTally Tally { get; } = new();

        /// <summary>Abre un periodo con saque alterno (periodNumber 1 = local).</summary>
        public void BeginPeriod(int periodNumber) => HomeOnOffense = periodNumber % 2 == 1;

        /// <summary>Cierra la posesión y entrega el balón al otro equipo.</summary>
        public void NextPossession() => HomeOnOffense = !HomeOnOffense;
    }

    /// <summary>
    /// Faltas del partido (F2-03): faltas de equipo por periodo (bonus) y
    /// expulsiones por acumulación. Los contadores se reinician en cada periodo,
    /// como en la NBA.
    /// </summary>
    public class FoulState
    {
        /// <summary>Falta de equipo que activa el bonus (tiros libres) en el periodo.</summary>
        public const int TEAM_FOUL_LIMIT = 4;
        /// <summary>Bonus en los dos últimos minutos del 4.º cuarto y de las prórrogas.</summary>
        public const int CLUTCH_TEAM_FOUL_LIMIT = 2;
        /// <summary>Faltas personales que expulsan al jugador.</summary>
        public const int PERSONAL_FOUL_LIMIT = 6;

        public int homeTeamFouls { get; private set; }
        public int awayTeamFouls { get; private set; }

        /// <summary>Reinicia el cómputo de faltas de equipo del periodo.</summary>
        public void BeginPeriod(int periodNumber)
        {
            homeTeamFouls = 0;
            awayTeamFouls = 0;
        }

        public int TeamFouls(bool home) => home ? homeTeamFouls : awayTeamFouls;

        /// <summary>Suma una falta al equipo y devuelve el total del periodo.</summary>
        public int AddTeamFoul(bool home)
        {
            if (home) homeTeamFouls++;
            else awayTeamFouls++;
            return TeamFouls(home);
        }
    }

    /// <summary>
    /// Visión de las faltas del equipo que <b>defiende</b> la posesión en curso
    /// (F2-03). `RegisterFoul()` cuenta la falta y dice si deja al atacante en
    /// bonus, es decir, si la falta sin tiro se paga con tiros libres.
    /// </summary>
    public class DefenseFouls
    {
        readonly FoulState _state;
        readonly bool _defenseIsHome;
        readonly bool _clutch;

        public DefenseFouls(FoulState state, bool defenseIsHome, bool clutch)
        {
            _state = state;
            _defenseIsHome = defenseIsHome;
            _clutch = clutch;
        }

        public int Limit => _clutch ? FoulState.CLUTCH_TEAM_FOUL_LIMIT : FoulState.TEAM_FOUL_LIMIT;
        public int TeamFouls => _state?.TeamFouls(_defenseIsHome) ?? 0;

        /// <summary>Cuenta la falta y devuelve true si el rival queda en bonus.</summary>
        public bool RegisterFoul()
        {
            if (_state == null) return false;
            return _state.AddTeamFoul(_defenseIsHome) >= Limit;
        }
    }

    public class GameResult
    {
        public GameData game;
        public int home_score, away_score;
        public List<(int home, int away)> quarters = new();
        public List<PlayerStatSnapshot> home_stats = new();
        public List<PlayerStatSnapshot> away_stats = new();
        public TeamStats home_team_stats = new();
        public TeamStats away_team_stats = new();
        public List<(int player_id, string type, int days)> injuries = new();
        public List<PlayByPlayEvent> playByPlay = new();
        /// <summary>Posesiones y ataques del partido (diagnóstico F2-01).</summary>
        public PossessionTally possessions = new();
        /// <summary>Algún equipo saltó a la pista con menos de cinco jugadores sanos
        /// (F2-06). El llamador puede avisar al jugador.</summary>
        public bool shortRoster;
        /// <summary>Un equipo no pudo presentarse: derrota 0–20 sin estadísticas de
        /// jugador (F2-06).</summary>
        public bool walkover;
    }

    /// <summary>
    /// Simula un partido. El laboratorio puede activar `simulatePlayerStateWhenNotPersisting`
    /// para evolucionar fatiga/lesiones en los objetos recibidos sin abrir ni escribir DB;
    /// en ese modo la lista de jugadores queda mutada en memoria al terminar.
    /// </summary>
    public static GameResult SimulateGame(GameData game, List<PlayerData> homePlayers, List<PlayerData> awayPlayers,
        int homeChemistry = 50, int awayChemistry = 50, bool persistToDb = true, bool glLeague = false,
        GamePlan? homePlan = null, GamePlan? awayPlan = null,
        int homeCoachBonus = 0, int awayCoachBonus = 0, bool capturePlayByPlay = false,
        IRandomSource random = null, bool simulatePlayerStateWhenNotPersisting = false)
    {
        random ??= UnityRandomSource.Instance;
        var hp = homePlan ?? GamePlan.Neutral;
        var ap = awayPlan ?? GamePlan.Neutral;
        var homePS = homePlayers.Where(p => p.injury_days == 0).Select(InitPS).ToList();
        var awayPS = awayPlayers.Where(p => p.injury_days == 0).Select(InitPS).ToList();
        PrepareRotationMetadata(homePS, glLeague);
        PrepareRotationMetadata(awayPS, glLeague);

        // Un plan se respeta si es manual (ROTACIÓN) o estratégico automático. La G-League
        // y el All-Star mantienen sus reglas específicas y no reciben este plan automático.
        bool strictHome = homePS.Any(p => p.planned && p.target_minutes > 0);
        bool strictAway = awayPS.Any(p => p.planned && p.target_minutes > 0);

        // Plantilla corta (F2-06): NUNCA se inventa el marcador. Con 1–4 jugadores sanos
        // se juega con lo que hay y el box score sale de la simulación real; solo si un
        // equipo no tiene NINGÚN jugador sano se resuelve por incomparecencia (0–20,
        // como en la NBA). Los avisos viajan en el `GameResult`.
        bool homeShort = homePS.Count < 5;
        bool awayShort = awayPS.Count < 5;
        bool walkover = homePS.Count == 0 || awayPS.Count == 0;

        if (homeShort || awayShort)
            Debug.LogWarning($"[Sim] Plantilla corta (local={homePS.Count}, visitante={awayPS.Count}): "
                + (walkover ? "incomparecencia 0–20" : "se simula con los disponibles"));

        if (walkover)
        {
            int homeScore = homePS.Count == 0 ? 0 : 20;
            int awayScore = awayPS.Count == 0 ? 0 : 20;
            if (homeScore == awayScore) homeScore = 20;   // ninguno se presenta: gana el local
            // Los 20 puntos reglamentarios se reparten 5 por cuarto; el equipo que no se
            // presenta deja los cuatro cuartos a cero (no se genera box score).
            bool homeForfeits = homeScore == 0;
            var walkoverQs = new List<(int, int)>
            {
                (homeForfeits ? 0 : 5, homeForfeits ? 5 : 0),
                (homeForfeits ? 0 : 5, homeForfeits ? 5 : 0),
                (homeForfeits ? 0 : 5, homeForfeits ? 5 : 0),
                (homeForfeits ? 0 : 5, homeForfeits ? 5 : 0)
            };
            game.home_score = homeScore;
            game.away_score = awayScore;
            game.is_played = 1;
            game.q1_home = walkoverQs[0].Item1; game.q1_away = walkoverQs[0].Item2;
            game.q2_home = walkoverQs[1].Item1; game.q2_away = walkoverQs[1].Item2;
            game.q3_home = walkoverQs[2].Item1; game.q3_away = walkoverQs[2].Item2;
            game.q4_home = walkoverQs[3].Item1; game.q4_away = walkoverQs[3].Item2;
            return new GameResult
            {
                game = game,
                home_score = homeScore,
                away_score = awayScore,
                quarters = walkoverQs,
                home_stats = homePS,
                away_stats = awayPS,
                home_team_stats = CalcTeamStats(homePS),
                away_team_stats = CalcTeamStats(awayPS),
                shortRoster = true,
                walkover = true
            };
        }

        int homeR = (int)homePlayers.Average(p => Mathf.Clamp(p.overall + (p.morale - 50) * 0.1f, 0, 99));
        int awayR = (int)awayPlayers.Average(p => Mathf.Clamp(p.overall + (p.morale - 50) * 0.1f, 0, 99));

        // Ventaja de casa: SIEMPRE para el equipo local real. Antes solo se aplicaba
        // cuando el equipo del usuario jugaba en casa, lo que sesgaba los resultados
        // (los partidos IA-IA eran neutrales y el local IA no recibía nada).
        // El bonus de química es simétrico para ambos equipos.
        float homeChemBonus = (homeChemistry - 50) * 0.15f;
        float awayChemBonus = (awayChemistry - 50) * 0.15f;
        const float homeCourtBonus = 1.5f;

        // Bonus del cuerpo técnico (calidad del asistente en el equipo del usuario).
        homeR = Mathf.Clamp(Mathf.RoundToInt(homeR + homeChemBonus + homeCourtBonus + homeCoachBonus), 0, 99);
        awayR = Mathf.Clamp(Mathf.RoundToInt(awayR + awayChemBonus + awayCoachBonus), 0, 99);
        float pace = Mathf.Clamp(BASE_PACE + (homeR + awayR - 140) * 0.06f + random.Range(-2f, 2f)
                                 + GamePlan.PaceAdjustment(hp, ap) + SpeedPaceDelta(homePS, awayPS), 92, 110);

        var quarters = new List<(int, int)>();
        // F2-08: la crónica solo se construye si el llamador la va a mostrar. En modo
        // directo no se gastan snapshots, diffs ni memoria en algo que se descarta.
        var playByPlay = capturePlayByPlay ? new List<PlayByPlayEvent>() : null;
        var possessionState = new PossessionState();
        var foulState = new FoulState();
        int homeTotal = 0, awayTotal = 0;

        for (int q = 0; q < 4; q++)
        {
            playByPlay?.Add(new PlayByPlayEvent
            {
                quarter = q + 1,
                text = $"Comienza el cuarto {q + 1}",
                homeScore = homeTotal,
                awayScore = awayTotal,
                timeElapsed = 0
            });
            var (hPts, aPts) = SimQuarter(q + 1, homeR, awayR, homePS, awayPS, pace, playByPlay, homeTotal, awayTotal, glLeague, strictHome, strictAway, hp, ap, possessionState, foulState, random);
            homeTotal += hPts; awayTotal += aPts;
            quarters.Add((hPts, aPts));
        }

        int otCount = 0;
        // Prórrogas hasta desempatar. El tope (10) es una red de seguridad: si se
        // alcanza, se resuelve con un punto para el local. El resultado NUNCA puede
        // quedar empatado (standings/brackets asumen un ganador).
        while (homeTotal == awayTotal && otCount < 10)
        {
            otCount++;
            playByPlay?.Add(new PlayByPlayEvent
            {
                quarter = 4 + otCount,
                text = "¡A la prórroga!",
                homeScore = homeTotal,
                awayScore = awayTotal,
                timeElapsed = 0
            });
            var (hOT, aOT) = SimOvertime(4 + otCount, homeR, awayR, homePS, awayPS, playByPlay, homeTotal, awayTotal, glLeague, strictHome, strictAway, hp, ap, possessionState, foulState, random);
            homeTotal += hOT; awayTotal += aOT;
            quarters.Add((hOT, aOT));
        }

        if (homeTotal == awayTotal)
        {
            homeTotal += 1;
            playByPlay?.Add(new PlayByPlayEvent
            {
                quarter = 4 + otCount,
                text = "Canasta final del local (desempate).",
                homeScore = homeTotal,
                awayScore = awayTotal,
                timeElapsed = 0
            });
        }

        playByPlay?.Add(new PlayByPlayEvent
        {
            quarter = 4 + otCount,
            text = "¡Fin del partido!",
            homeScore = homeTotal,
            awayScore = awayTotal,
            timeElapsed = 4 + otCount <= 4 ? 12 : 5
        });

        game.home_score = homeTotal;
        game.away_score = awayTotal;
        game.is_played = 1;

        // Guardar quarter scores en DB
        var qsArr = quarters.ToArray();
        game.q1_home = qsArr.Length > 0 ? qsArr[0].Item1 : 0;
        game.q1_away = qsArr.Length > 0 ? qsArr[0].Item2 : 0;
        game.q2_home = qsArr.Length > 1 ? qsArr[1].Item1 : 0;
        game.q2_away = qsArr.Length > 1 ? qsArr[1].Item2 : 0;
        game.q3_home = qsArr.Length > 2 ? qsArr[2].Item1 : 0;
        game.q3_away = qsArr.Length > 2 ? qsArr[2].Item2 : 0;
        game.q4_home = qsArr.Length > 3 ? qsArr[3].Item1 : 0;
        game.q4_away = qsArr.Length > 3 ? qsArr[3].Item2 : 0;

        // El box score es la suma de las acciones del partido (F2-05): no hay pisos
        // artificiales de asistencias/rebotes/robos/tapones por atributo. El rating y
        // los dobles-dobles se calculan sobre lo que el jugador hizo de verdad.
        foreach (var ps in homePS.Concat(awayPS))
        {
            ps.minutes = Mathf.Round(ps.minutes);
            ps.rating = ps.points + ps.oreb + ps.dreb + ps.assists + ps.steals + ps.blocks
                        - (ps.fga - ps.fgm) - (ps.fta - ps.ftm) - ps.turnovers - ps.pf;
            int cats = 0;
            if (ps.points >= 10) cats++;
            if (ps.oreb + ps.dreb >= 10) cats++;
            if (ps.assists >= 10) cats++;
            if (ps.steals >= 10) cats++;
            if (ps.blocks >= 10) cats++;
            if (cats >= 3) { ps.triple_double = 1; ps.double_double = 0; }
            else if (cats >= 2) { ps.double_double = 1; ps.triple_double = 0; }
        }

        // Guardar estadísticas para TODOS los tipos de partido
        // (la pantalla Stats filtra solo temporada regular).
        // Los partidos G-League no escriben en player_game_stats: sus stats
        // viven en gleague_season_stats y las gestiona el llamador.
        // El All-Star SÍ persiste su boxscore, con los team_id de conferencia (-1/-2) que
        // usa su GameData, para que MatchDay/GameResults lo muestren. Las consultas de
        // carrera/medias filtran por game_type='regular', así que no cuentan como partido
        // jugado ni entran en el histórico (ver GetGamesPlayedByPlayerInSeason y
        // UpdateHistoricalPlayerStatsFromSeason).
        if (persistToDb)
        {
            DatabaseManager.Instance.DeletePlayerGameStatsForGame(game.id);
            // Solo se persisten los jugadores que realmente jugaron: las filas con
            // 0 minutos contaban como partido jugado e inflaban GP/medias.
            // F2-09: las filas se acumulan y se insertan en lote (una sola transacción).
            var statRows = new List<PlayerGameStats>();
            foreach (var ps in homePS)
                if (ps.minutes > 0)
                    statRows.Add(PS2DB(ps, game.id, game.home_team_id));
            foreach (var ps in awayPS)
                if (ps.minutes > 0)
                    statRows.Add(PS2DB(ps, game.id, game.away_team_id));
            DatabaseManager.Instance.SavePlayerGameStatsBatch(statRows);
        }

        // F2-09: los jugadores que cambian tras el partido (físico/lesión) se acumulan
        // aquí y se escriben una sola vez al final, en vez de un Update por jugador.
        var playersById = new Dictionary<int, PlayerData>();
        foreach (var p in homePlayers) playersById[p.id] = p;
        foreach (var p in awayPlayers) playersById[p.id] = p;
        var changedPlayers = new Dictionary<int, PlayerData>();

        // Check and update records (exclude All-Star — no records should count)
        if (persistToDb && game.game_type != "allstar"
            && game.game_type != CupHelper.PlayoffGameType)
        {
            DatabaseManager.Instance.CheckAndUpdateRecords(game, homePS, game.home_team_id);
            DatabaseManager.Instance.CheckAndUpdateRecords(game, awayPS, game.away_team_id);
        }

        // Fatiga post-partido (los partidos G-League no desgastan el físico:
        // los prospectos ni existen en players, y a los asignados no se les
        // aplica doble desgaste por jugar en dos competiciones). El All-Star es
        // una exhibición: tampoco desgasta.
        if ((persistToDb || simulatePlayerStateWhenNotPersisting) && game.game_type != "allstar")
        {
            bool homeBackToBack = persistToDb && DatabaseManager.Instance.Db.Table<GameData>()
                .Any(g => (g.home_team_id == game.home_team_id || g.away_team_id == game.home_team_id)
                       && g.is_played == 1 && g.game_day == game.game_day - 1);
            bool awayBackToBack = persistToDb && DatabaseManager.Instance.Db.Table<GameData>()
                .Any(g => (g.home_team_id == game.away_team_id || g.away_team_id == game.away_team_id)
                       && g.is_played == 1 && g.game_day == game.game_day - 1);
            foreach (var ps in homePS)
            {
                if (!playersById.TryGetValue(ps.player_id, out var player) || player == null) continue;
                int loss = Mathf.RoundToInt(ps.minutes * 0.28f);
                if (homeBackToBack) loss = Mathf.RoundToInt(loss * 1.5f);
                player.fisico = Mathf.Max(0, player.fisico - loss);
                changedPlayers[player.id] = player;
            }
            foreach (var ps in awayPS)
            {
                if (!playersById.TryGetValue(ps.player_id, out var player) || player == null) continue;
                int loss = Mathf.RoundToInt(ps.minutes * 0.28f);
                if (awayBackToBack) loss = Mathf.RoundToInt(loss * 1.5f);
                player.fisico = Mathf.Max(0, player.fisico - loss);
                changedPlayers[player.id] = player;
            }
        }

        // Sin lesiones en el All-Star (exhibición).
        bool trackInjuries = (persistToDb || simulatePlayerStateWhenNotPersisting) && game.game_type != "allstar";
        var homeInjuries = trackInjuries
            ? CheckInjuries(homePS, playersById, changedPlayers, random)
            : new List<(int player_id, string type, int days)>();
        var awayInjuries = trackInjuries
            ? CheckInjuries(awayPS, playersById, changedPlayers, random)
            : new List<(int player_id, string type, int days)>();

        // F2-09: una sola escritura para todos los cambios de jugadores del partido.
        if (persistToDb && changedPlayers.Count > 0)
            DatabaseManager.Instance.UpdatePlayersBatch(new List<PlayerData>(changedPlayers.Values));

        var result = new GameResult
        {
            game = game,
            home_score = homeTotal,
            away_score = awayTotal,
            quarters = quarters,
            home_stats = homePS,
            away_stats = awayPS,
            home_team_stats = CalcTeamStats(homePS),
            away_team_stats = CalcTeamStats(awayPS),
            injuries = homeInjuries.Concat(awayInjuries).ToList(),
            playByPlay = playByPlay,
            possessions = possessionState.Tally,
            shortRoster = homeShort || awayShort
        };
        return result;
    }

    static PlayerStatSnapshot InitPS(PlayerData p)
    {
        return new PlayerStatSnapshot
        {
            player_id = p.id,
            name = $"{p.first_name} {p.last_name}",
            position = p.position,
            secondary_position = p.secondary_position,
            role = p.role,
            overall = p.overall,
            shooting = p.shooting,
            three_point = p.three_point,
            passing = p.passing,
            rebounding = p.rebounding,
            defense = p.defense,
            steals_attr = p.steals,
            blocks_attr = p.blocks,
            speed = p.speed,
            dribbling = p.dribbling,
            athleticism = p.athleticism,
            iq = p.iq,
            minutes = 0,
            target_minutes = p.rotation_minutes,
            fixed_minutes = p.rotation_minutes >= 0,
            fisico = p.fisico
        };
    }

    // Suelo de minutos para una rotación configurada: un plan entre 1 y 7 se sube a 8
    // (0 se respeta: el jugador no entra salvo necesidad).
    /// <summary>Suelo de minutos de un plan fijado (y del fondo del banquillo).</summary>
    public const int MIN_FIXED_MINUTES = 2;

    /// <summary>
    /// Índices del quinteto inicial. Con plan vinculante (strict) un titular con
    /// plan 0 no debe empezar el partido: se siembra con los que tienen minutos
    /// pendientes manteniendo el orden de titulares y se completan los huecos con el
    /// resto. Sin strict, los 5 primeros por orden (titulares de la convocatoria).
    /// </summary>
    public static List<int> InitialOnCourt(List<PlayerStatSnapshot> ps, bool strict)
    {
        var result = new List<int>();
        int need = Mathf.Min(5, ps.Count);

        if (strict)
        {
            for (int i = 0; i < ps.Count && result.Count < need; i++)
                if (ps[i].target_minutes > 0)
                    result.Add(i);
        }
        for (int i = 0; i < ps.Count && result.Count < need; i++)
            if (!result.Contains(i))
                result.Add(i);
        return result;
    }

    public static void PrepareRotationMetadata(List<PlayerStatSnapshot> players, bool glLeague = false)
    {
        if (players == null || players.Count == 0) return;

        // Sin ningún plan positivo (p. ej. tras RESET a 0) la rotación vuelve a ser
        // automática: se ignoran los 0 configurados y se usa la heurística.
        if (!players.Any(p => p.fixed_minutes && p.target_minutes > 0))
        {
            for (int i = 0; i < players.Count; i++)
            {
                players[i].fixed_minutes = false;
                players[i].target_minutes = -1;
            }
        }

        for (int i = 0; i < players.Count; i++)
            players[i].starter = i < 5;

        // F2-11 · Rotación estratégica automática (IA y equipo del usuario sin configurar):
        // los 240 minutos se reparten de una vez con los ideales de rol, nivel y forma
        // física. Es el mismo reparto que muestra la pantalla ROTACIÓN (`AutoAssign` sobre
        // `GetAutomaticMinutes`), así que lo que se ve es lo que se juega, y la moral deja
        // de medir a cada jugador contra una promesa que nadie había calculado.
        // La G-League mantiene la heurística anterior (reparto más corto, sin pantalla).
        if (!glLeague && !players.Any(p => p.fixed_minutes && p.target_minutes > 0))
        {
            var ideals = new List<int>(players.Count);
            for (int i = 0; i < players.Count; i++)
                ideals.Add(GetAutomaticMinutes(players[i]));

            var assigned = RotationPlanner.AutoAssign(ideals);
            for (int i = 0; i < players.Count; i++)
            {
                players[i].target_minutes = assigned[i];
                players[i].planned = true;   // vinculante: el simulador respeta el plan
            }
            return;
        }

        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].fixed_minutes)
            {
                // Minutos configurados en ROTACIÓN: vinculantes. No los toca la fatiga
                // (para eso está la gestión de carga), solo el suelo mínimo.
                if (players[i].target_minutes > 0 && players[i].target_minutes < MIN_FIXED_MINUTES)
                    players[i].target_minutes = MIN_FIXED_MINUTES;
                players[i].planned = players[i].target_minutes > 0;
                continue;
            }

            // Sin configurar (-1 = automático) dentro de un plan parcial: heurística por
            // rol/OVR (el hueco que deja el usuario lo reparte la IA).
            if (players[i].target_minutes < 0)
                players[i].target_minutes = GetTargetMinutes(players[i], glLeague);
            if (players[i].fisico < 30)
                players[i].target_minutes = Mathf.Max(MIN_FIXED_MINUTES, players[i].target_minutes - 6);
        }
    }

    /// <summary>
    /// Ideal de minutos de un jugador en rotación automática (F2-11): promesa del rol,
    /// nivel dentro del rol y forma física. `RotationPlanner.AutoAssign` lo normaliza a los
    /// 240 del partido. Es la misma función que alimenta la columna SUGERIDO de la pantalla
    /// ROTACIÓN, de modo que el reparto mostrado coincide con el que se juega.
    /// </summary>
    public static int GetAutomaticMinutes(PlayerStatSnapshot player, bool glLeague = false)
    {
        // Punto de partida por rol (la moral usa la misma tabla como expectativa:
        // Estrella 40 / Titular 28 / Banquillo 10 / Último 3).
        int minutes = player.role switch
        {
            PlayerRole.Estrella => 34,
            PlayerRole.Titular => 27,
            PlayerRole.Banquillo => 15,
            _ => MIN_FIXED_MINUTES
        };

        // Nivel real dentro del rol: un 90 juega más que un 75.
        if (player.overall >= 90) minutes += 4;
        else if (player.overall >= 85) minutes += 2;
        else if (player.overall < 75) minutes -= 3;

        // Forma física: quien llega cansado juega menos y el reparto compensa con otros.
        if (player.fisico < 40) minutes = Mathf.RoundToInt(minutes * 0.80f);
        else if (player.fisico < 60) minutes = Mathf.RoundToInt(minutes * 0.90f);

        // El quinteto inicial arranca con algo más de carga.
        if (player.starter) minutes += 2;

        if (glLeague) return Mathf.Max(MIN_FIXED_MINUTES, Mathf.RoundToInt(minutes * 0.75f));
        return Mathf.Clamp(minutes, MIN_FIXED_MINUTES, 42);
    }

    public static int GetTargetMinutes(PlayerStatSnapshot player, bool glLeague = false)
    {
        // Tabla por media: las estrellas juegan 40, los titulares de nivel medio ~30 y el
        // fondo del banquillo poco. El rol solo decide el tramo bajo (Último recurso = suelo).
        int minutes;
        if (player.role == PlayerRole.UltimoRecurso) minutes = MIN_FIXED_MINUTES;
        else if (player.starter)
        {
            if (player.overall >= 90) minutes = 40;
            else if (player.overall >= 85) minutes = 34;
            else if (player.overall >= 80) minutes = 33;
            else if (player.overall >= 75) minutes = 26;
            else minutes = 20;
        }
        else
        {
            if (player.overall >= 90) minutes = 32;
            else if (player.overall >= 85) minutes = 28;
            else if (player.overall >= 80) minutes = 22;
            else if (player.overall >= 75) minutes = 14;
            else minutes = 12;
        }

        // G-League: reparto algo más corto (los prospectos comparten más carga).
        if (glLeague) return Mathf.Max(MIN_FIXED_MINUTES, Mathf.RoundToInt(minutes * 0.75f));
        return minutes;
    }

    /// <summary>Minutos que sugeriría el simulador a un jugador (columna SUGERIDO de la
    /// pantalla ROTACIÓN): la misma función que usa la rotación automática del partido,
    /// así que incluye rol, nivel y forma física.</summary>
    public static int GetSuggestedMinutes(PlayerData p, bool starter)
    {
        return GetAutomaticMinutes(new PlayerStatSnapshot
        {
            role = p.role,
            overall = p.overall,
            starter = starter,
            fisico = p.fisico
        });
    }

    static PlayerGameStats PS2DB(PlayerStatSnapshot ps, int gameId, int teamId)
    {
        return new PlayerGameStats
        {
            game_id = gameId,
            player_id = ps.player_id,
            team_id = teamId,
            minutes = ps.minutes,
            points = ps.points,
            fgm = ps.fgm,
            fga = ps.fga,
            fg3m = ps.fg3m,
            fg3a = ps.fg3a,
            ftm = ps.ftm,
            fta = ps.fta,
            oreb = ps.oreb,
            dreb = ps.dreb,
            rebounds = ps.oreb + ps.dreb,
            assists = ps.assists,
            steals = ps.steals,
            blocks = ps.blocks,
            turnovers = ps.turnovers,
            pf = ps.pf,
            rating = ps.rating,
            double_double = ps.double_double,
            triple_double = ps.triple_double,
            fisico_start = ps.fisico
        };
    }

    static (int, int) SimQuarter(int qNum, int homeR, int awayR, List<PlayerStatSnapshot> homePS, List<PlayerStatSnapshot> awayPS, float pace, List<PlayByPlayEvent> log, int baseHome = 0, int baseAway = 0, bool glLeague = false, bool strictHome = false, bool strictAway = false, GamePlan homePlan = default, GamePlan awayPlan = default, PossessionState possession = null,
        FoulState fouls = null, IRandomSource random = null)
    {
        random ??= UnityRandomSource.Instance;
        possession ??= new PossessionState();
        possession.BeginPeriod(qNum);
        fouls ??= new FoulState();
        fouls.BeginPeriod(qNum);
        var tally = possession.Tally;
        int homePts = 0, awayPts = 0;
        int teamPoss = Mathf.Clamp(Mathf.RoundToInt(pace / 4 * random.Range(0.96f, 1.04f)), 22, 28);
        int totalPoss = teamPoss * 2;

        var homeMins = new float[homePS.Count];
        var awayMins = new float[awayPS.Count];
        var homeOn = new HashSet<int>(InitialOnCourt(homePS, strictHome));
        var awayOn = new HashSet<int>(InitialOnCourt(awayPS, strictAway));
        // Calendario de cambios independiente por equipo: las ventanas densas son para
        // cualquier equipo con plan (manual o estratégico); el resto usa las de siempre.
        var homeSchedule = SubSchedule(qNum, strictHome);
        var awaySchedule = SubSchedule(qNum, strictAway);
        int homeSubIdx = 0;
        int awaySubIdx = 0;
        float minsPerPoss = 12f / totalPoss;

        // El saque del periodo lo decide `PossessionState.BeginPeriod` (alterno), no
        // una moneda. Desde ahí el balón solo cambia al cerrar cada posesión.
        for (int pIdx = 0; pIdx < totalPoss; pIdx++)
        {
            float elapsed = pIdx * minsPerPoss;
            if (homeSubIdx < homeSchedule.Length && elapsed >= homeSchedule[homeSubIdx])
            {
                homeOn = DoSub(homeOn, homePS, homeMins, strictHome, random);
                homeSubIdx++;
            }
            if (awaySubIdx < awaySchedule.Length && elapsed >= awaySchedule[awaySubIdx])
            {
                awayOn = DoSub(awayOn, awayPS, awayMins, strictAway, random);
                awaySubIdx++;
            }
            foreach (int i in homeOn) homeMins[i] += minsPerPoss;
            foreach (int i in awayOn) awayMins[i] += minsPerPoss;

            bool homeOnOffense = possession.HomeOnOffense;
            if (homeOnOffense) tally.homePossessions++;
            else tally.awayPossessions++;
            var (possPts, attacks) = PlayPossession(homeOnOffense, homeOn, awayOn, homePS, awayPS, homeR, awayR,
                glLeague, homePlan, awayPlan, log, qNum, baseHome + homePts, baseAway + awayPts, elapsed, minsPerPoss,
                fouls, IsClutch(qNum, elapsed), random);
            if (homeOnOffense) { homePts += possPts; tally.homeAttacks += attacks; }
            else { awayPts += possPts; tally.awayAttacks += attacks; }
            possession.NextPossession();
        }

        // F3-05: fase final. Un cuarto se juega un número fijo de posesiones, así que el
        // reloj no puede alargarse solo. Si al cerrar el 4.º cuarto el partido sigue
        // abierto (1-6 puntos), la fase final añade posesiones alternando al que va por
        // detrás y al líder (última posesión / juego de faltas con posesión corta). Se
        // corta al decidirse el partido: empate (prórroga) o remontada del que estaba
        // por detrás. No se añaden tiros libres: el volumen de FT ya está calibrado
        // (F3-04) y el reloj solo se alarga con posesiones reales.
        if (qNum == 4)
        {
            float lateElapsed = totalPoss * minsPerPoss;
            // El margen relevante es el del PARTIDO (marcador acumulado), no el del cuarto.
            int lateMargin = (baseHome + homePts) - (baseAway + awayPts);
            bool attackerIsHome = lateMargin < 0;   // la fase la abre el que va por detrás
            for (int extra = 0; extra < LATE_EXTENSION_MAX_POSS; extra++)
            {
                if (lateMargin == 0 || Mathf.Abs(lateMargin) > LATE_EXTENSION_MAX_MARGIN) break;

                lateElapsed += minsPerPoss;
                float lateClock = Mathf.Min(lateElapsed, 11.98f);
                foreach (int i in homeOn) homeMins[i] += minsPerPoss;
                foreach (int i in awayOn) awayMins[i] += minsPerPoss;
                if (attackerIsHome) tally.homePossessions++;
                else tally.awayPossessions++;
                tally.lateExtensionPossessions++;

                var (latePts, lateAttacks) = PlayPossession(attackerIsHome, homeOn, awayOn, homePS, awayPS,
                    homeR, awayR, glLeague, homePlan, awayPlan, log, qNum, baseHome + homePts, baseAway + awayPts,
                    lateClock, minsPerPoss, fouls, IsClutch(qNum, lateClock), random);
                if (attackerIsHome) { homePts += latePts; tally.homeAttacks += lateAttacks; }
                else { awayPts += latePts; tally.awayAttacks += lateAttacks; }

                int newMargin = (baseHome + homePts) - (baseAway + awayPts);
                if (newMargin == 0) break;                              // empate → prórroga
                if (Mathf.Sign(newMargin) != Mathf.Sign(lateMargin)) break;  // remontada
                lateMargin = newMargin;
                attackerIsHome = !attackerIsHome;
            }
        }

        for (int i = 0; i < homePS.Count; i++) homePS[i].minutes += homeMins[i];
        for (int i = 0; i < awayPS.Count; i++) awayPS[i].minutes += awayMins[i];

        return (homePts, awayPts);
    }

    static (int, int) SimOvertime(int qNum, int homeR, int awayR, List<PlayerStatSnapshot> homePS, List<PlayerStatSnapshot> awayPS, List<PlayByPlayEvent> log, int baseHome = 0, int baseAway = 0, bool glLeague = false, bool strictHome = false, bool strictAway = false, GamePlan homePlan = default, GamePlan awayPlan = default, PossessionState possession = null,
        FoulState fouls = null, IRandomSource random = null)
    {
        random ??= UnityRandomSource.Instance;
        possession ??= new PossessionState();
        possession.BeginPeriod(qNum);
        fouls ??= new FoulState();
        fouls.BeginPeriod(qNum);
        var tally = possession.Tally;
        int homePts = 0, awayPts = 0;
        int totalPoss = 24;
        var homeMins = new float[homePS.Count];
        var awayMins = new float[awayPS.Count];
        var homeOn = new HashSet<int>(InitialOnCourt(homePS, strictHome));
        var awayOn = new HashSet<int>(InitialOnCourt(awayPS, strictAway));
        float minsPerPoss = 5f / totalPoss;
        float[] subSchedule = { 2.5f };
        int subIdx = 0;

        for (int pIdx = 0; pIdx < totalPoss; pIdx++)
        {
            float elapsed = pIdx * minsPerPoss;
            if (subIdx < subSchedule.Length && elapsed >= subSchedule[subIdx])
            {
                homeOn = DoSub(homeOn, homePS, homeMins, strictHome, random);
                awayOn = DoSub(awayOn, awayPS, awayMins, strictAway, random);
                subIdx++;
            }
            foreach (int i in homeOn) homeMins[i] += minsPerPoss;
            foreach (int i in awayOn) awayMins[i] += minsPerPoss;

            bool homeOnOffense = possession.HomeOnOffense;
            if (homeOnOffense) tally.homePossessions++;
            else tally.awayPossessions++;
            var (possPts, attacks) = PlayPossession(homeOnOffense, homeOn, awayOn, homePS, awayPS, homeR, awayR,
                glLeague, homePlan, awayPlan, log, qNum, baseHome + homePts, baseAway + awayPts, elapsed, minsPerPoss,
                fouls, IsClutch(qNum, elapsed), random);
            if (homeOnOffense) { homePts += possPts; tally.homeAttacks += attacks; }
            else { awayPts += possPts; tally.awayAttacks += attacks; }
            possession.NextPossession();
        }

        for (int i = 0; i < homePS.Count; i++) homePS[i].minutes += homeMins[i];
        for (int i = 0; i < awayPS.Count; i++) awayPS[i].minutes += awayMins[i];

        return (homePts, awayPts);
    }

    static float FisicoPenalty(int fisico)
    {
        if (fisico >= 30) return 1f;
        return 0.75f + (fisico / 30f) * 0.25f;
    }

    /// <summary>Factor de compresión para la G-League: acerca los pesos de uso/rebote
    /// hacia la media del quinteto, evitando que una estrella NBA (OVR ~80) domine por
    /// completo frente a prospectos (~50) — p.ej. 40 PPP / 18 RPP. 0 = reparto
    /// totalmente igualitario (~20% cada uno); 1 = sin cambios.</summary>
    const float GL_COMPRESS = 0.2f;

    /// <summary>Acerca cada peso hacia la media de la lista (conserva el orden pero
    /// reduce el abismo estrella/resto). Solo se usa en partidos G-League.</summary>
    static void CompressWeights(List<float> weights, float factor)
    {
        if (weights == null || weights.Count < 2) return;
        float mean = weights.Sum() / weights.Count;
        for (int i = 0; i < weights.Count; i++)
            weights[i] = Mathf.Max(0.0001f, mean + (weights[i] - mean) * factor);
    }

    static void AddLog(List<PlayByPlayEvent> log, int quarter, string desc, int homeScore, int awayScore, float elapsed, List<StatDelta> deltas = null)
    {
        if (log == null || string.IsNullOrEmpty(desc)) return;
        log.Add(new PlayByPlayEvent
        {
            quarter = quarter,
            text = desc,
            homeScore = homeScore,
            awayScore = awayScore,
            timeElapsed = elapsed,
            deltas = deltas
        });
    }

    struct BoxSnapshot
    {
        public int player_id;
        public float minutes;
        public int points, fgm, fga, fg3m, fg3a, ftm, fta, oreb, dreb, assists, steals, blocks, turnovers, pf;
    }

    static BoxSnapshot SnapPlayer(PlayerStatSnapshot p)
    {
        return new BoxSnapshot
        {
            player_id = p.player_id,
            minutes = p.minutes,
            points = p.points, fgm = p.fgm, fga = p.fga,
            fg3m = p.fg3m, fg3a = p.fg3a, ftm = p.ftm, fta = p.fta,
            oreb = p.oreb, dreb = p.dreb, assists = p.assists,
            steals = p.steals, blocks = p.blocks, turnovers = p.turnovers, pf = p.pf
        };
    }

    static List<BoxSnapshot> CaptureBox(List<PlayerStatSnapshot> homePS, List<PlayerStatSnapshot> awayPS)
    {
        var snap = new List<BoxSnapshot>(homePS.Count + awayPS.Count);
        for (int i = 0; i < homePS.Count; i++) snap.Add(SnapPlayer(homePS[i]));
        for (int i = 0; i < awayPS.Count; i++) snap.Add(SnapPlayer(awayPS[i]));
        return snap;
    }

    static List<StatDelta> DiffBox(List<BoxSnapshot> before, List<BoxSnapshot> after)
    {
        var deltas = new List<StatDelta>();
        if (before == null || after == null || before.Count != after.Count) return deltas;
        for (int i = 0; i < before.Count; i++)
        {
            var b = before[i]; var a = after[i];
            AddD(deltas, a.player_id, "pts", a.points - b.points);
            AddD(deltas, a.player_id, "fgm", a.fgm - b.fgm);
            AddD(deltas, a.player_id, "fga", a.fga - b.fga);
            AddD(deltas, a.player_id, "fg3m", a.fg3m - b.fg3m);
            AddD(deltas, a.player_id, "fg3a", a.fg3a - b.fg3a);
            AddD(deltas, a.player_id, "ftm", a.ftm - b.ftm);
            AddD(deltas, a.player_id, "fta", a.fta - b.fta);
            AddD(deltas, a.player_id, "oreb", a.oreb - b.oreb);
            AddD(deltas, a.player_id, "dreb", a.dreb - b.dreb);
            AddD(deltas, a.player_id, "ast", a.assists - b.assists);
            AddD(deltas, a.player_id, "stl", a.steals - b.steals);
            AddD(deltas, a.player_id, "blk", a.blocks - b.blocks);
            AddD(deltas, a.player_id, "to", a.turnovers - b.turnovers);
            AddD(deltas, a.player_id, "pf", a.pf - b.pf);
        }
        return deltas;
    }

    static void AddD(List<StatDelta> deltas, int player_id, string stat, float amount)
    {
        if (amount != 0) deltas.Add(new StatDelta { player_id = player_id, stat = stat, amount = amount });
    }

    /// <summary>Tope de ataques dentro de una misma posesión (rebotes ofensivos
    /// encadenados). Es una red de seguridad: 3 segundas oportunidades ya es raro.</summary>
    public const int MAX_ATTACKS_PER_POSSESSION = 4;

    /// <summary>
    /// Ejecuta una posesión con segundas oportunidades: llama a `attack` y repite
    /// mientras la jugada acabe en rebote ofensivo (el mismo equipo sigue atacando),
    /// hasta `maxAttacks`. Devuelve los ataques usados (*possessions used*), que son
    /// ≥ 1 y solo superan 1 cuando hay rebotes ofensivos.
    /// Es pública y pura respecto al azar para poder testear el control de la
    /// posesión con secuencias guionizadas.
    /// </summary>
    public static int RunPossessionChain(Func<PossessionOutcome> attack, int maxAttacks = MAX_ATTACKS_PER_POSSESSION)
    {
        if (attack == null) return 0;
        int attacks = 0;
        bool keepGoing;
        do
        {
            var outcome = attack();
            attacks++;
            keepGoing = outcome != null && outcome.offensiveRebound && attacks < maxAttacks;
        } while (keepGoing);
        return attacks;
    }

    /// <summary>
    /// Juega una posesión completa para `home` o `away`: resuelve los ataques con
    /// segundas oportunidades, acumula puntos y registra cada ataque en el
    /// play-by-play. Devuelve los puntos anotados y los ataques usados.
    /// </summary>
    static (int points, int attacks) PlayPossession(bool homeOnOffense, HashSet<int> homeOn, HashSet<int> awayOn,
        List<PlayerStatSnapshot> homePS, List<PlayerStatSnapshot> awayPS,
        int homeR, int awayR, bool glLeague, GamePlan homePlan, GamePlan awayPlan,
        List<PlayByPlayEvent> log, int qNum, int preHomeScore, int preAwayScore, float elapsed,
        float minsPerPoss, FoulState fouls = null, bool clutch = false, IRandomSource random = null)
    {
        random ??= UnityRandomSource.Instance;
        int points = 0;
        int attacksSoFar = 0;
        // Las faltas se le cuentan al equipo que defiende (F2-03).
        var defenseFouls = new DefenseFouls(fouls, !homeOnOffense, clutch);
        int attacks = RunPossessionChain(() =>
        {
            // F2-08: en modo directo (log == null) no se sacan snapshots ni se calculan
            // deltas; solo se simula la jugada.
            var before = log != null ? CaptureBox(homePS, awayPS) : null;
            int currentHomeScore = homeOnOffense ? preHomeScore + points : preHomeScore;
            int currentAwayScore = homeOnOffense ? preAwayScore : preAwayScore + points;
            int trailingMargin = Mathf.Abs(currentHomeScore - currentAwayScore);
            bool trailingLateGame = qNum == 4 && elapsed >= 9f
                && trailingMargin > 0 && trailingMargin <= 6
                && (homeOnOffense ? currentHomeScore < currentAwayScore : currentAwayScore < currentHomeScore);
            var outcome = homeOnOffense
                ? RunPossession(homeOn, awayOn, homePS, awayPS, homeR, awayR, glLeague, homePlan, awayPlan, defenseFouls, random, trailingLateGame)
                : RunPossession(awayOn, homeOn, awayPS, homePS, awayR, homeR, glLeague, awayPlan, homePlan, defenseFouls, random, trailingLateGame);
            points += outcome.pts;

            if (log != null)
            {
                var deltas = DiffBox(before, CaptureBox(homePS, awayPS));
                if (attacksSoFar == 0)
                {
                    // El hueco de tiempo de la posesión se anota una sola vez, aunque un
                    // rebote ofensivo añada ataques dentro de la misma posesión.
                    foreach (int i in homeOn) deltas.Add(new StatDelta { player_id = homePS[i].player_id, stat = "min", amount = minsPerPoss });
                    foreach (int i in awayOn) deltas.Add(new StatDelta { player_id = awayPS[i].player_id, stat = "min", amount = minsPerPoss });
                }
                AddLog(log, qNum, outcome.desc,
                    homeOnOffense ? preHomeScore + points : preHomeScore,
                    homeOnOffense ? preAwayScore : preAwayScore + points,
                    elapsed, deltas);
            }

            attacksSoFar++;
            return outcome;
        });

        // Expulsiones por seis faltas: el equipo sigue con otro jugador de la banca.
        EjectFouledOut(homeOn, homePS, log, qNum, homeOnOffense ? preHomeScore + points : preHomeScore,
            homeOnOffense ? preAwayScore : preAwayScore + points, elapsed);
        EjectFouledOut(awayOn, awayPS, log, qNum, homeOnOffense ? preHomeScore + points : preHomeScore,
            homeOnOffense ? preAwayScore : preAwayScore + points, elapsed);

        return (points, attacks);
    }

    /// <summary>Últimos dos minutos del 4.º cuarto o de la prórroga: el bonus de
    /// faltas de equipo llega antes (F2-03).</summary>
    public static bool IsClutch(int periodNumber, float elapsedMinutes)
    {
        if (periodNumber < 4) return false;
        // Cuartos de 12' y prórrogas de 5'.
        return periodNumber == 4 ? elapsedMinutes >= 10f : elapsedMinutes >= 3f;
    }

    static PossessionOutcome RunPossession(HashSet<int> offIds, HashSet<int> defIds, List<PlayerStatSnapshot> offAll, List<PlayerStatSnapshot> defAll, int offR, int defR, bool glLeague = false, GamePlan offPlan = default, GamePlan defPlan = default,
        DefenseFouls defFouls = null, IRandomSource random = null, bool trailingLateGame = false)
    {
        random ??= UnityRandomSource.Instance;
        var off = offIds.Select(i => offAll[i]).ToList();
        var def = defIds.Select(i => defAll[i]).ToList();

        // La defensa agresiva fuerza más pérdidas; la conservadora menos. El bote del
        // quinteto protege el balón (F2-04).
        float toPct = TurnoverChance(offR, defR, BallSecurity(off), defPlan);
        if (random.Value < toPct)
        {
            string toName = DoTO(off, def, random);
            return new PossessionOutcome
            {
                pts = 0,
                desc = $"{toName ?? "Pérdida de balón"} pierde el balón"
            };
        }

        // Falta sin tiro en la penetración (F2-03). Cuenta como falta de equipo: si
        // deja al atacante en bonus, se paga con dos tiros libres; si no, el ataque
        // continúa desde el saque (misma posesión, sin ataque extra).
        if (random.Value < ReachInFoulChance(defPlan))
        {
            string foulName = DoFoul(def, random);
            if (foulName != null && defFouls != null && defFouls.RegisterFoul())
            {
                var ftShooter = PickShooter(off, glLeague, random);
                if (ftShooter != null)
                {
                    int made = ShootFreeThrows(ftShooter, 2, out bool live, random);
                    var outcome = new PossessionOutcome
                    {
                        pts = made,
                        desc = made > 0
                            ? $"{ftShooter.name} convierte {made}/2 tiros libres (bonus; falta de {foulName})"
                            : $"{ftShooter.name} falla los 2 tiros libres (bonus; falta de {foulName})"
                    };
                    if (live) AttachLiveRebound(outcome, def, off, glLeague, random);
                    return outcome;
                }
            }
        }

        var shooter = PickShooter(off, glLeague, random);
        if (shooter == null)
            return new PossessionOutcome { pts = 0, desc = "Pérdida" };

        string shot = ShotType(shooter, offPlan, random, trailingLateGame);
        var defender = def.OrderByDescending(p => p.defense).FirstOrDefault();
        float di = defender != null ? (defender.defense - 70) * 0.005f : 0;

        if (shot == "3")
        {
            float fp3 = FisicoPenalty(shooter.fisico);
            // F3-04: el efecto del nivel individual se comprime alrededor del OVR medio
            // del seed; evita que el roster sintético más fuerte abra un FG% irreal.
            float basePct = (BASE_THREE_POINT_PERCENT
                + (shooter.three_point - 70) * THREE_POINT_ATTRIBUTE_SLOPE) * fp3;
            float pct = Mathf.Clamp(basePct - di, 0.28f, 0.51f);
            shooter.fg3a++; shooter.fga++;
            if (random.Value < pct)
            {
                shooter.fg3m++; shooter.fgm++; shooter.points += 3;
                string astr = DoAst(off, shooter, glLeague, random);
                return new PossessionOutcome
                {
                    pts = 3,
                    desc = astr != null
                        ? $"{shooter.name} encesta un triple (asist. {astr})"
                        : $"{shooter.name} encesta un triple"
                };
            }
            return MissHandler(def, off, shooter, true, glLeague, defPlan, defFouls, random);
        }

        float fp2 = FisicoPenalty(shooter.fisico);
        float base2Pct = (BASE_TWO_POINT_PERCENT
            + (shooter.shooting - 70) * TWO_POINT_ATTRIBUTE_SLOPE) * fp2;
        float pct2 = Mathf.Clamp(base2Pct - di * 0.25f + FinishingBonus(shooter.athleticism), 0.40f, 0.67f);
        shooter.fga++;
        if (random.Value < pct2)
        {
            shooter.fgm++; shooter.points += 2;
            string astr = DoAst(off, shooter, glLeague, random);
            if (random.Value < 0.06f)
            {
                string foulName = DoFoul(def, random);
                defFouls?.RegisterFoul();
                int bonusMade = ShootFreeThrows(shooter, 1, out bool liveAndOne, random);
                var andOne = new PossessionOutcome
                {
                    pts = 2 + bonusMade,
                    desc = bonusMade > 0
                        ? $"{shooter.name} anota y añade el tiro libre (falta de {foulName ?? "defensa"})"
                        : $"{shooter.name} anota de dos pero falla el tiro adicional"
                };
                if (liveAndOne) AttachLiveRebound(andOne, def, off, glLeague, random);
                return andOne;
            }
            return new PossessionOutcome
            {
                pts = 2,
                desc = astr != null
                    ? $"{shooter.name} anota de dos (asist. {astr})"
                    : $"{shooter.name} anota de dos"
            };
        }
        return MissHandler(def, off, shooter, false, glLeague, defPlan, defFouls, random);
    }

    static PossessionOutcome MissHandler(List<PlayerStatSnapshot> def, List<PlayerStatSnapshot> off, PlayerStatSnapshot shooter, bool isThree, bool glLeague = false, GamePlan defPlan = default, DefenseFouls defFouls = null,
        IRandomSource random = null)
    {
        random ??= UnityRandomSource.Instance;
        // Un solo intento colectivo: el defensor se elige por atributo y posición,
        // evitando que el orden de la lista convierta a un base en taponador principal.
        var blockWeights = def
            .Select(d => Mathf.Clamp((d.blocks_attr - 60) / 800f, 0, 0.05f)
                * GetBlockPositionMultiplier(d.position))
            .ToList();
        float totalBlockChance = Mathf.Clamp(blockWeights.Sum(), 0f, 0.12f);
        if (random.Value < totalBlockChance && totalBlockChance > 0)
        {
            float roll = random.Value * blockWeights.Sum();
            float cumulative = 0;
            for (int i = 0; i < def.Count; i++)
            {
                cumulative += blockWeights[i];
                if (roll <= cumulative)
                {
                    def[i].blocks++;
                    return new PossessionOutcome
                    {
                        pts = 0,
                        desc = $"{def[i].name} tapona el tiro de {shooter.name}"
                    };
                }
            }
        }

        float foulChance = (isThree ? 0.12f : 0.08f) + defPlan.FoulDelta();
        if (random.Value < foulChance)
        {
            string foulName = DoFoul(def, random);
            // Toda falta cuenta para el bonus de equipo; las de tiro pagan libres igual.
            defFouls?.RegisterFoul();
            int nShots = isThree ? 3 : 2;
            int made = ShootFreeThrows(shooter, nShots, out bool liveRebound, random);
            var outcome = new PossessionOutcome
            {
                pts = made,
                desc = made > 0
                    ? $"{shooter.name} convierte {made}/{nShots} tiros libres (falta de {foulName ?? "defensa"})"
                    : $"{shooter.name} falla los {nShots} tiros libres"
            };
            // El último libre fallado deja el balón vivo (F2-03).
            if (liveRebound) AttachLiveRebound(outcome, def, off, glLeague, random);
            return outcome;
        }

        string rebName = DoReb(def, off, out bool offensiveRebound, glLeague, random);
        return new PossessionOutcome
        {
            pts = 0,
            desc = rebName == null
                ? "Rebote capturado"
                : offensiveRebound
                    ? $"{rebName} captura el rebote ofensivo (sigue la posesión)"
                    : $"{rebName} captura el rebote",
            offensiveRebound = offensiveRebound
        };
    }

    static PlayerStatSnapshot PickShooter(List<PlayerStatSnapshot> court, bool glLeague = false, IRandomSource random = null)
    {
        random ??= UnityRandomSource.Instance;
        if (court.Count == 0) return null;
        var weights = court.Select(p => Mathf.Pow(p.overall / 100f, 2.2f) * FisicoPenalty(p.fisico)).ToList();
        if (glLeague) CompressWeights(weights, GL_COMPRESS);
        float total = weights.Sum();
        if (total <= 0) return court[random.Range(0, court.Count)];
        float r = random.Value * total;
        float cum = 0;
        for (int i = 0; i < court.Count; i++)
        {
            cum += weights[i];
            if (r <= cum) return court[i];
        }
        return court[^1];
    }

    public static string ShotType(PlayerStatSnapshot p, GamePlan plan = default, IRandomSource random = null,
        bool trailingLateGame = false)
    {
        random ??= UnityRandomSource.Instance;
        if (p == null) return "2";
        float base3pt = p.position switch
        {
            "PG" => 0.45f,
            "SG" => 0.51f,
            "SF" => 0.43f,
            "PF" => 0.38f,
            "C" => 0.23f,
            _ => 0.35f
        };
        // En los últimos tres minutos, si va perdiendo por seis o menos, el ataque busca
        // más triples para maximizar la opción de igualar; no se altera el marcador ni se
        // fuerza una prórroga (F3-05).
        float lateThreePointBias = trailingLateGame ? 0.12f : 0f;
        float adj = Mathf.Clamp(base3pt + (p.three_point - 75) * 0.002f + plan.ThreePointBias()
            + lateThreePointBias, 0.10f, 0.67f);
        // El IQ elige el tiro que más le conviene (F2-04): si su triple es mejor que
        // su tiro de dos, insiste desde fuera; si es peor, se mete hacia dentro.
        adj += (p.three_point - p.shooting) * 0.0015f * Mathf.Clamp01(p.iq / 100f);
        adj *= FisicoPenalty(p.fisico);
        return random.Value < adj ? "3" : "2";
    }

    static string DoAst(List<PlayerStatSnapshot> court, PlayerStatSnapshot scorer, bool glLeague = false,
        IRandomSource random = null)
    {
        random ??= UnityRandomSource.Instance;
        // En la G-League hay más canastas asistidas (reparto más coral del ataque):
        // sin el reparto extra, los líderes no llegan ni a 4 APP porque el star
        // acapara los tiros y quedan pocas canastas ajenas que asistir.
        // % de canastas asistidas: ~55% en la NBA real (antes 35%, las asistencias
        // salían estructuralmente bajas). La G-League reparte aún más.
        if (random.Value >= (glLeague ? 0.62f : BASE_ASSISTED_FIELD_GOAL_CHANCE)) return null;
        var others = court.Where(p => p != scorer).ToList();
        if (others.Count == 0) return null;
        var w = others.Select(p => Mathf.Pow(Mathf.Max(1, p.passing), 3f)).ToList();
        float t = w.Sum();
        if (t <= 0) return null;
        float r = random.Value * t;
        float c = 0;
        for (int i = 0; i < others.Count; i++)
        {
            c += w[i];
            if (r <= c) { others[i].assists++; return others[i].name; }
        }
        return null;
    }

    static string DoReb(List<PlayerStatSnapshot> def, List<PlayerStatSnapshot> off, out bool offensive,
        bool glLeague = false, IRandomSource random = null)
    {
        random ??= UnityRandomSource.Instance;
        offensive = false;
        var dw = def.Select(p => ReboundWeight(p, false)).ToList();
        var ow = off.Select(p => ReboundWeight(p, true)).ToList();
        if (glLeague)
        {
            CompressWeights(dw, GL_COMPRESS);
            CompressWeights(ow, GL_COMPRESS);
        }
        float dSum = dw.Sum();
        float oSum = ow.Sum();
        float t = dSum + oSum;
        if (t <= 0) return null;

        if (random.Value * t < dSum)
            return AwardReb(def, dw, true, random);
        offensive = true;
        return AwardReb(off, ow, false, random);
    }

    static string AwardReb(List<PlayerStatSnapshot> players, List<float> weights, bool defensive,
        IRandomSource random = null)
    {
        random ??= UnityRandomSource.Instance;
        if (players.Count == 0) return null;
        float s = weights.Sum();
        if (s <= 0) return null;
        float r = random.Value * s;
        float c = 0;
        for (int i = 0; i < players.Count; i++)
        {
            c += weights[i];
            if (r <= c)
            {
                if (defensive) players[i].dreb++;
                else players[i].oreb++;
                return players[i].name;
            }
        }
        return null;
    }

    static float ReboundWeight(PlayerStatSnapshot player, bool offensive)
    {
        // Los pesos de ambos equipos deben compartir exponente; comparar reb^2.5 en
        // ataque con reb^3 en defensa introduce una desventaja artificial de ~8x
        // (atributo 80), dejando el rebote ofensivo muy por debajo del 25% NBA.
        // La escala 0.33 aproxima la proporción real de rebotes ofensivos.
        float weight = Mathf.Pow(Mathf.Max(1, player.rebounding), 3f)
            * GetReboundPositionMultiplier(player.position, offensive)
            * ReboundAthleticismFactor(player.athleticism);
        return offensive ? weight * OFFENSIVE_REBOUND_WEIGHT_SCALE : weight;
    }

    /// <summary>El atletismo pesa en la pelea por el rebote (F2-04).</summary>
    public static float ReboundAthleticismFactor(int athleticism)
        => Mathf.Clamp(1f + (athleticism - 70) * 0.004f, 0.7f, 1.4f);

    /// <summary>Efecto del atletismo en la finalización cerca del aro (F2-04):
    /// ±3,6 puntos porcentuales por cada 20 puntos de atributo.</summary>
    public static float FinishingBonus(int athleticism) => (athleticism - 70) * 0.0018f;

    /// <summary>Seguridad de balón del quinteto (F2-04): bote medio de los
    /// manejadores; por debajo de 70 se pierden más balones.</summary>
    public static float BallSecurity(IEnumerable<PlayerStatSnapshot> court)
    {
        if (court == null) return 70f;
        var list = court.Where(p => p != null).ToList();
        if (list.Count == 0) return 70f;
        var handlers = list.Where(p => p.position is "PG" or "SG" or "SF").ToList();
        if (handlers.Count == 0) handlers = list;
        return handlers.Average(p => (float)p.dribbling);
    }

    /// <summary>Probabilidad de pérdida de la posesión (F2-04): diferencia de
    /// nivel, plan defensivo y bote del ataque.</summary>
    public static float TurnoverChance(int offRating, int defRating, float ballSecurity, GamePlan defPlan = default)
    {
        float pct = BASE_TURNOVER_CHANCE + (defRating - offRating) * 0.0003f + defPlan.ForcedTurnoverDelta();
        pct -= (ballSecurity - 70f) * 0.0015f;
        return Mathf.Clamp(pct, 0.02f, 0.30f);
    }

    /// <summary>Efecto de la velocidad en el ritmo del partido (F2-04): el equipo
    /// rápido juega a más posesiones.</summary>
    public static float SpeedPaceDelta(List<PlayerStatSnapshot> homePS, List<PlayerStatSnapshot> awayPS)
    {
        float sum = 0f;
        int count = 0;
        foreach (var p in homePS ?? new List<PlayerStatSnapshot>()) { sum += p.speed; count++; }
        foreach (var p in awayPS ?? new List<PlayerStatSnapshot>()) { sum += p.speed; count++; }
        float avg = count == 0 ? 70f : sum / count;
        return (avg - 70f) * 0.15f;
    }

    public static float GetReboundPositionMultiplier(string position, bool offensive)
    {
        if (offensive)
        {
            return position switch
            {
                "PG" => 0.35f,
                "SG" => 0.50f,
                "SF" => 0.75f,
                "PF" => 1.20f,
                "C" => 1.40f,
                _ => 1f
            };
        }

        return position switch
        {
            "PG" => 0.45f,
            "SG" => 0.55f,
            "SF" => 0.80f,
            "PF" => 1.20f,
            "C" => 1.35f,
            _ => 1f
        };
    }

    public static float GetBlockPositionMultiplier(string position)
    {
        return position switch
        {
            "PG" => 0.20f,
            "SG" => 0.30f,
            "SF" => 0.55f,
            "PF" => 1.15f,
            "C" => 1.30f,
            _ => 1f
        };
    }

    static string DoTO(List<PlayerStatSnapshot> off, List<PlayerStatSnapshot> def, IRandomSource random = null)
    {
        random ??= UnityRandomSource.Instance;
        string toName = null;
        var handlers = off.Where(p => p.position is "PG" or "SG" or "SF").ToList();
        if (handlers.Count == 0) handlers = off;
        if (handlers.Count > 0)
        {
            var toWeights = handlers.Select(p => 1f / Mathf.Max(0.1f, FisicoPenalty(p.fisico))).ToList();
            float toTotal = toWeights.Sum();
            float toR = random.Value * toTotal;
            float toC = 0;
            for (int i = 0; i < handlers.Count; i++)
            {
                toC += toWeights[i];
                if (toR <= toC) { handlers[i].turnovers++; toName = handlers[i].name; break; }
            }
        }

        // Only ~50% of TOs result in a steal (rest are offensive fouls, out-of-bounds, etc.)
        if (random.Value >= 0.5f) return toName;
        if (def.Count == 0) return toName;
        var stlW = def.Select(p => Mathf.Pow(p.steals_attr, 3)).ToList();
        float t = stlW.Sum();
        if (t > 0)
        {
            float r = random.Value * t;
            float c = 0;
            for (int i = 0; i < def.Count; i++)
            {
                c += stlW[i];
                if (r <= c) { def[i].steals++; return toName; }
            }
        }
        def.OrderByDescending(p => p.steals_attr).First().steals++;
        return toName;
    }

    /// <summary>Probabilidad de falta sin tiro en la penetración (F2-03).</summary>
    static float ReachInFoulChance(GamePlan defPlan) => Mathf.Max(0f, BASE_REACH_IN_FOUL_CHANCE + defPlan.FoulDelta());

    /// <summary>
    /// Tira `shots` libres y devuelve los anotados. `liveRebound` indica que el
    /// ÚLTIMO tiro libre se falló, así que el balón queda vivo y hay pelea por el
    /// rebote (F2-03). Si el último entra, el balón muere.
    /// </summary>
    public static int ShootFreeThrows(PlayerStatSnapshot shooter, int shots, out bool liveRebound,
        IRandomSource random = null)
    {
        random ??= UnityRandomSource.Instance;
        liveRebound = false;
        if (shooter == null) return 0;
        float pct = 0.75f + (shooter.overall - 70) * 0.002f;
        int made = 0;
        for (int i = 0; i < shots; i++)
        {
            shooter.fta++;
            if (random.Value < pct)
            {
                shooter.ftm++; shooter.points++; made++;
            }
            else if (i == shots - 1)
            {
                liveRebound = true;
            }
        }
        return made;
    }

    /// <summary>Resuelve el rebote de un último tiro libre fallado y lo añade a la
    /// jugada: si es ofensivo, la posesión sigue (segunda oportunidad).</summary>
    static void AttachLiveRebound(PossessionOutcome outcome, List<PlayerStatSnapshot> def, List<PlayerStatSnapshot> off, bool glLeague = false, IRandomSource random = null)
    {
        random ??= UnityRandomSource.Instance;
        if (outcome == null) return;
        string rebName = DoReb(def, off, out bool offensiveRebound, glLeague, random);
        outcome.offensiveRebound = offensiveRebound;
        if (rebName == null) return;
        outcome.desc += offensiveRebound
            ? $"; {rebName} captura el rebote ofensivo (sigue la posesión)"
            : $"; {rebName} captura el rebote";
    }

    /// <summary>Reparto de faltas personales: los interiores faltan más y quien ya
    /// está en problemas de faltas defiende con más cuidado (comete menos).</summary>
    public static float FoulWeight(PlayerStatSnapshot player)
    {
        if (player == null || player.pf >= FoulState.PERSONAL_FOUL_LIMIT) return 0f;
        float pos = player.position switch
        {
            "C" => 2.0f,
            "PF" => 1.6f,
            "SF" => 1.1f,
            "SG" => 0.9f,
            "PG" => 0.8f,
            _ => 1f
        };
        return pos * Mathf.Max(0.35f, 1f - player.pf * 0.10f);
    }

    static string DoFoul(List<PlayerStatSnapshot> court, IRandomSource random = null)
    {
        random ??= UnityRandomSource.Instance;
        if (court == null || court.Count == 0) return null;
        var weights = court.Select(FoulWeight).ToList();
        float total = weights.Sum();
        if (total <= 0)
        {
            court[0].pf++;
            return court[0].name;
        }

        float r = random.Value * total;
        float c = 0;
        for (int i = 0; i < court.Count; i++)
        {
            c += weights[i];
            if (r <= c)
            {
                court[i].pf++;
                return court[i].name;
            }
        }
        court[^1].pf++;
        return court[^1].name;
    }

    /// <summary>
    /// Expulsa a los jugadores con seis faltas y mete al mejor suplente disponible
    /// (por encaje posicional con el expulsado, minutos pendientes y OVR). Si no
    /// queda nadie en la banca, el equipo sigue con menos jugadores.
    /// Devuelve los nombres expulsados.
    /// </summary>
    public static List<string> EjectFouledOut(HashSet<int> on, List<PlayerStatSnapshot> all, ICollection<int> unavailable = null)
    {
        var ejected = new List<string>();
        if (on == null || all == null) return ejected;

        foreach (int index in on.ToList())
        {
            if (index < 0 || index >= all.Count) continue;
            if (all[index].pf < FoulState.PERSONAL_FOUL_LIMIT) continue;

            ejected.Add(all[index].name);
            on.Remove(index);

            int replacement = PickFoulReplacement(on, all, all[index], unavailable);
            if (replacement >= 0) on.Add(replacement);
        }
        return ejected;
    }

    static int PickFoulReplacement(HashSet<int> on, List<PlayerStatSnapshot> all, PlayerStatSnapshot ejected, ICollection<int> unavailable)
    {
        int best = -1, bestFit = -1;
        float bestNeed = float.MinValue;
        int bestOvr = -1;

        for (int i = 0; i < all.Count; i++)
        {
            if (on.Contains(i)) continue;
            if (unavailable != null && unavailable.Contains(i)) continue;
            var candidate = all[i];
            if (candidate.pf >= FoulState.PERSONAL_FOUL_LIMIT) continue;
            if (candidate.fixed_minutes && candidate.target_minutes <= 0) continue;

            int fit = PositionFit(candidate, ejected);
            float need = candidate.target_minutes - candidate.minutes;
            if (fit > bestFit
                || (fit == bestFit && need > bestNeed)
                || (fit == bestFit && need == bestNeed && candidate.overall > bestOvr))
            {
                best = i; bestFit = fit; bestNeed = need; bestOvr = candidate.overall;
            }
        }
        return best;
    }

    /// <summary>Expulsa a los que llegan a seis faltas y lo cuenta en el play-by-play.</summary>
    static void EjectFouledOut(HashSet<int> on, List<PlayerStatSnapshot> all, List<PlayByPlayEvent> log,
        int quarter, int homeScore, int awayScore, float elapsed)
    {
        foreach (string name in EjectFouledOut(on, all))
            AddLog(log, quarter, $"{name} comete su sexta falta y deja la pista", homeScore, awayScore, elapsed);
    }

    static HashSet<int> DoSub(HashSet<int> on, List<PlayerStatSnapshot> players, float[] quarterMinutes,
        bool strict = false, IRandomSource random = null)
    {
        random ??= UnityRandomSource.Instance;
        int n = Mathf.Min(random.Value < 0.65f ? 1 : 2, on.Count);
        if (n == 0) return on;

        var newSet = new HashSet<int>(on);
        for (int change = 0; change < n; change++)
        {
            var pick = PickSubstitution(newSet, players, quarterMinutes, strict);
            if (pick.outgoing < 0 || pick.incoming < 0) break;

            newSet.Remove(pick.outgoing);
            newSet.Add(pick.incoming);
        }

        return newSet;
    }

    /// <summary>Decide un cambio (sale / entra) en una ventana de substitución. Es puro
    /// y determinista para poder testearlo. Con <paramref name="strict"/> = true se
    /// respeta el plan de minutos (manual o automático).</summary>
    public static (int outgoing, int incoming) PickSubstitution(
        ICollection<int> on, List<PlayerStatSnapshot> players, float[] quarterMinutes, bool strict)
    {
        int maxPlayers = players.Count;
        if (on.Count == 0) return (-1, -1);

        int outgoing;
        if (strict)
        {
            // Sale quien más se pasa de su objetivo; si nadie se pasa, quien más
            // proporción de su plan lleva (para liberar minutos a quien espera).
            outgoing = on
                .Where(i => CanRemoveFromRotation(on, players, i, quarterMinutes, strict))
                .OrderByDescending(i => Overage(players[i], quarterMinutes[i]))
                .ThenByDescending(i => MinutesRatio(players[i], quarterMinutes[i]))
                .ThenBy(i => players[i].target_minutes)
                .ThenBy(i => players[i].overall)
                .DefaultIfEmpty(-1)
                .First();
        }
        else
        {
            outgoing = on
                .Where(i => CanRemoveFromRotation(on, players, i, quarterMinutes, strict))
                .OrderByDescending(i => MinutesRatio(players[i], quarterMinutes[i]))
                .ThenBy(i => players[i].target_minutes)
                .ThenBy(i => players[i].overall)
                .DefaultIfEmpty(-1)
                .First();
        }
        if (outgoing < 0) return (-1, -1);

        int incoming;
        if (strict)
        {
            // Entra quien tiene minutos pendientes, ponderando el encaje posicional.
            incoming = Enumerable.Range(0, maxPlayers)
                .Where(i => !on.Contains(i))
                .Where(i => players[i].pf < FoulState.PERSONAL_FOUL_LIMIT)
                .Where(i => Remaining(players[i], quarterMinutes[i]) > 0.01f)
                .OrderByDescending(i => Remaining(players[i], quarterMinutes[i]) * FitFactor(players[i], players[outgoing]))
                .ThenByDescending(i => Remaining(players[i], quarterMinutes[i]))
                .ThenByDescending(i => players[i].overall)
                .DefaultIfEmpty(-1)
                .First();
        }
        else
        {
            incoming = Enumerable.Range(0, maxPlayers)
                .Where(i => !on.Contains(i))
                .Where(i => players[i].pf < FoulState.PERSONAL_FOUL_LIMIT)
                .OrderByDescending(i => PositionFit(players[i], players[outgoing]))
                .ThenByDescending(i => MinutesNeed(players[i], quarterMinutes[i]))
                .ThenByDescending(i => players[i].overall)
                .DefaultIfEmpty(-1)
                .First();
        }
        if (incoming < 0) return (-1, -1);

        return (outgoing, incoming);
    }

    static float MinutesRatio(PlayerStatSnapshot player, float currentQuarterMinutes)
    {
        float current = player.minutes + currentQuarterMinutes;
        return player.target_minutes > 0 ? current / player.target_minutes : current;
    }

    static float MinutesNeed(PlayerStatSnapshot player, float currentQuarterMinutes)
    {
        return Mathf.Max(0, player.target_minutes - player.minutes - currentQuarterMinutes);
    }

    // Minutos pendientes según el plan configurado (puede ser negativo si se pasó).
    static float Remaining(PlayerStatSnapshot player, float currentQuarterMinutes)
    {
        return player.target_minutes - player.minutes - currentQuarterMinutes;
    }

    // Minutos jugados por encima del plan (negativo si aún le quedan).
    static float Overage(PlayerStatSnapshot player, float currentQuarterMinutes)
    {
        return player.minutes + currentQuarterMinutes - player.target_minutes;
    }

    // Encaje posicional del que entra respecto al que sale, como factor del plan:
    // posición exacta 1.0, mismo tipo interior/exterior 0.85, ninguno 0.6.
    static float FitFactor(PlayerStatSnapshot incoming, PlayerStatSnapshot outgoing)
    {
        return PositionFit(incoming, outgoing) switch
        {
            3 => 1.00f,
            2 => 0.85f,
            _ => 0.60f
        };
    }

    static bool CanRemoveFromRotation(ICollection<int> on, List<PlayerStatSnapshot> players, int index, float[] quarterMinutes, bool strict)
    {
        var player = players[index];
        // Con plan vinculante se usan los minutos efectivos (player.minutes solo se
        // actualiza al cerrar el cuarto); en automático sin plan se usa el acumulado global.
        float played = player.minutes;
        if (strict && quarterMinutes != null && index < quarterMinutes.Length)
            played += quarterMinutes[index];

        bool isStar = player.role == PlayerRole.Estrella || player.overall >= 85;
        if (isStar && player.starter && played < player.target_minutes * 0.55f)
            return false;

        if (player.position is "C" or "PF")
        {
            int interiors = on.Count(i => players[i].position is "C" or "PF");
            if (interiors <= 1)
                return false;
        }

        return true;
    }

    static int PositionFit(PlayerStatSnapshot incoming, PlayerStatSnapshot outgoing)
    {
        if (PlaysPosition(incoming, outgoing.position)) return 3;

        bool incomingInterior = incoming.position is "C" or "PF";
        bool outgoingInterior = outgoing.position is "C" or "PF";
        if (incomingInterior == outgoingInterior) return 2;
        return 0;
    }

    static bool PlaysPosition(PlayerStatSnapshot player, string position)
    {
        return player.position == position || player.secondary_position == position;
    }

    static float[] SubSchedule(int qNum, bool dense = false)
    {
        if (dense)
        {
            // Ventanas cada 1,5 min: suficientes cambios para cumplir los planes
            // configurados (con las de siempre el quinteto inicial se quedaba +4 min).
            return new[] { 1.5f, 3f, 4.5f, 6f, 7.5f, 9f, 10.5f };
        }

        return qNum switch
        {
            1 => new[] { 4f, 8f },
            2 => new[] { 2f, 5f, 8f, 10.5f },
            3 => new[] { 4f, 8f },
            _ => new[] { 2f, 5f, 8f, 10.5f }
        };
    }

    static TeamStats CalcTeamStats(List<PlayerStatSnapshot> ps)
    {
        var ts = new TeamStats();
        foreach (var p in ps)
        {
            ts.fgm += p.fgm; ts.fga += p.fga;
            ts.fg3m += p.fg3m; ts.fg3a += p.fg3a;
            ts.ftm += p.ftm; ts.fta += p.fta;
            ts.oreb += p.oreb; ts.dreb += p.dreb;
            ts.assists += p.assists; ts.steals += p.steals;
            ts.blocks += p.blocks; ts.turnovers += p.turnovers;
            ts.pf += p.pf;
        }
        ts.reb = ts.oreb + ts.dreb;
        ts.points = ts.fg3m * 3 + (ts.fgm - ts.fg3m) * 2 + ts.ftm;
        return ts;
    }

    static List<(int, int)> DistributeQuarters(int homeTotal, int awayTotal, IRandomSource random)
    {
        var h = Partition(homeTotal, 4, 22, 40, random);
        var a = Partition(awayTotal, 4, 22, 40, random);
        return h.Zip(a, (x, y) => (x, y)).ToList();
    }

    static List<int> Partition(int total, int n, int lo, int hi, IRandomSource random)
    {
        var result = new List<int>();
        int remaining = total;
        for (int i = 0; i < n - 1; i++)
        {
            int low = Mathf.Max(lo, remaining - hi * (n - 1 - i));
            int high = Mathf.Min(hi, remaining - lo * (n - 1 - i));
            if (low > high)
            {
                low = Mathf.Max(lo, remaining / (n - i));
                high = low + 1;
            }
            int val = random.Range(low, high + 1);
            result.Add(val);
            remaining -= val;
        }
        result.Add(Mathf.Max(lo, remaining));
        return result;
    }

    static List<(int player_id, string type, int days)> CheckInjuries(
        List<PlayerStatSnapshot> players,
        Dictionary<int, PlayerData> playersById = null,
        Dictionary<int, PlayerData> changed = null, IRandomSource random = null)
    {
        random ??= UnityRandomSource.Instance;
        var injuries = new List<(int, string, int)>();
        foreach (var ps in players)
        {
            // SIM-06: quien no juega (0 minutos) no se lesiona. Antes la probabilidad era
            // plana y también caía sobre los que ni pisaron la pista.
            if (ps.minutes <= 0) continue;

            // F2-09: se usa el jugador ya cargado por el llamador (sin ir a la DB por
            // jugador); los cambios se acumulan para la escritura en lote.
            PlayerData player;
            if (playersById != null)
                playersById.TryGetValue(ps.player_id, out player);
            else
                player = DatabaseManager.Instance?.GetPlayerById(ps.player_id);
            if (player == null || player.injury_days > 0) continue;

            // F3-06: una lesión postpartido no puede dejar al equipo con menos de cinco
            // jugadores sanos para el siguiente encuentro. El recuento se actualiza
            // durante el lote, conforme se asignan las nuevas lesiones.
            int healthyAvailable = 0;
            foreach (var candidate in players)
            {
                PlayerData rosterPlayer;
                if (playersById != null)
                    playersById.TryGetValue(candidate.player_id, out rosterPlayer);
                else
                    rosterPlayer = DatabaseManager.Instance?.GetPlayerById(candidate.player_id);
                if (rosterPlayer != null && rosterPlayer.injury_days <= 0) healthyAvailable++;
            }
            if (!InjuryHelper.CanSustainMatchInjury(healthyAvailable)) continue;

            // E2: un jugador que volvió antes de tiempo (alta forzada) arrastra riesgo de
            // recaída; si se lesiona en ese estado, la lesión es una recaída más grave.
            // F2-07: el riesgo depende de la exposición (minutos), del físico y del riesgo
            // de recaída; 0 minutos ⇒ 0. El sorteo es postpartido (no cambia minutos ni
            // resultado): así lo cuenta la noticia de la bandeja.
            bool relapse = player.recovery_risk > 0;
            float injuryChance = InjuryHelper.MatchRiskPerGame(
                Mathf.RoundToInt(ps.minutes), ps.fisico, player.recovery_risk);
            if (random.Value >= injuryChance) continue;

            InjuryDef injury = relapse
                ? InjuryHelper.Relapse(InjuryCatalog.ByName(player.injury_type), random.Value)
                : InjuryCatalog.Pick(random.Value);
            int days = InjuryHelper.RollDays(injury, random.Value);
            player.injury_type = injury.name;
            player.injury_days = days;
            player.injury_total_days = days;
            player.treated = 0;
            player.recovery_risk = 0;
            if (changed != null) changed[player.id] = player;
            else DatabaseManager.Instance.UpdatePlayer(player);
            injuries.Add((ps.player_id, injury.name, days));
        }
        return injuries;
    }

}
