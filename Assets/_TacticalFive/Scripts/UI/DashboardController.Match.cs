using UnityEngine;
using UnityEngine.UIElements;
using SQLite;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading.Tasks;

// B47: Partido individual, G-League y manejo de errores del día.
public partial class DashboardController
{
    void ProcessSingleGame(GameData game)
    {
        // ── Partidos G-League: pipeline propio, sin tocar estado NBA ──
        if (IsGLeagueGame(game))
        {
            ProcessGLeagueGame(game);
            return;
        }

        List<PlayerData> homePlayers, awayPlayers;

        if (game.game_type == "allstar")
        {
            homePlayers = BuildAllStarRoster("East");
            awayPlayers = BuildAllStarRoster("West");
            // Exhibición: reparto explícito de los 240 minutos entre los convocados para
            // que jueguen todos (con la heurística por rol solo entraban 6-7). Ignora la
            // rotación de los equipos reales.
            AssignAllStarMinutes(homePlayers);
            AssignAllStarMinutes(awayPlayers);
        }
        else
        {
            homePlayers = GetActivePlayers(game.home_team_id);
            awayPlayers = GetActivePlayers(game.away_team_id);
        }

        int homeChem = DatabaseManager.Instance.GetTeamChemistry(game.home_team_id);
        int awayChem = DatabaseManager.Instance.GetTeamChemistry(game.away_team_id);
        bool isMyHomeGame = game.home_team_id == _myTeam.id;

        var homeStarters = new HashSet<int>(homePlayers.Take(5).Select(p => p.id));
        var awayStarters = new HashSet<int>(awayPlayers.Take(5).Select(p => p.id));
        GameResultCache.GameStarters[game.id] = new HashSet<int>(homeStarters.Concat(awayStarters));

        var result = GameSimulator.SimulateGame(game, homePlayers, awayPlayers, homeChem, awayChem,
            homePlan: PlanFor(game.home_team_id), awayPlan: PlanFor(game.away_team_id),
            homeCoachBonus: CoachBonusFor(game.home_team_id), awayCoachBonus: CoachBonusFor(game.away_team_id),
            capturePlayByPlay: game.game_type != "allstar" && UIScreenController.GetSimMode() != UIScreenController.SimModeDirect);
        DatabaseManager.Instance.UpdateGame(game);
        GameResultCache.SimulatedGameIds.Add(game.id);

        // Plantilla corta (F2-06): el partido se simuló con los disponibles o se resolvió
        // por incomparecencia. Si afecta al equipo del jugador, se le avisa.
        if (result.shortRoster && game.game_type != "allstar"
            && (game.home_team_id == _myTeam.id || game.away_team_id == _myTeam.id))
            NotifyShortRoster(result, game);

        if (UIScreenController.GetSimMode() != UIScreenController.SimModeDirect && result.playByPlay != null)
            GameResultCache.PlayByPlayLogs[game.id] = result.playByPlay;

        if (game.game_type == "allstar")
        {
            var allStats = result.home_stats.Concat(result.away_stats).ToList();
            var mvpStats = allStats.OrderByDescending(s => s.rating).First();
            var record = new AllStarRecord
            {
                manager_id = _manager.id,
                season = $"{_season.year_end}",
                east_score = game.home_score,
                west_score = game.away_score,
                mvp = mvpStats.name,
                mvp_player_id = mvpStats.player_id
            };
            DatabaseManager.Instance.SaveAllStarRecord(record);

            // Convocatoria (sustituye a las stats del partido, que ya no se persisten).
            DatabaseManager.Instance.SaveAllStarRoster(_manager.id, _season.id, homePlayers, awayPlayers);

            bool alreadySent = DatabaseManager.Instance.Db.Table<MessageData>()
                .Any(m => m.manager_id == _manager.id && m.news_type == NewsType.ALLSTAR
                       && m.game_day == game.game_day);
            if (!alreadySent)
            {
                bool eastWon = game.home_score > game.away_score;
                int maxScore = Mathf.Max(game.home_score, game.away_score);
                int minScore = Mathf.Min(game.home_score, game.away_score);
                string winnerConf = eastWon ? "Conferencia Este" : "Conferencia Oeste";
                string loserConf = eastWon ? "Conferencia Oeste" : "Conferencia Este";
                DatabaseManager.Instance.AddMessage(new MessageData
                {
                    manager_id = _manager.id,
                    sender_type = 2,
                    sender_id = 0,
                    title = $"All-Star: {winnerConf} {maxScore}-{minScore} {loserConf}",
                    body = NewsTexts.AllStar(_season.year_end, eastWon, game.home_score,
                        game.away_score, mvpStats.name, mvpStats.points),
                    game_day = game.game_day,
                    game_date = game.game_date,
                    created_at = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    date_sent = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    is_read = 0,
                    category = NewsCategory.LIGA,
                    news_type = NewsType.ALLSTAR
                });
            }
        }

        if (game.game_type != "allstar")
            TeamEconomyService.ProcessGameFinances(game, _manager, _myTeam, _season);

        ProcessGameInjuries(result, game.game_date);

        bool myTeamInThisGame = game.home_team_id == _myTeam.id || game.away_team_id == _myTeam.id;
        if (myTeamInThisGame)
        {
            CreateGameResultMessage(game, result);
            UpdateFanConfidence(game, result);
            CheckBudgetAfterGame();
        }

        // Recalculate morale for all players in this game
        bool homeWon = game.home_score > game.away_score;
        UpdatePlayersMoraleAfterGame(result.home_stats, game.home_team_id, homeWon);
        UpdatePlayersMoraleAfterGame(result.away_stats, game.away_team_id, !homeWon);

        // Evolve relationships for teams in this game
        DatabaseManager.Instance.UpdateRelationshipsAfterGame(
            game.home_team_id, game.id, homeWon,
            result.home_stats.Where(s => s.minutes > 0).Select(s => s.player_id).ToList());
        DatabaseManager.Instance.UpdateRelationshipsAfterGame(
            game.away_team_id, game.id, !homeWon,
            result.away_stats.Where(s => s.minutes > 0).Select(s => s.player_id).ToList());
    }

    // ── AVISO DE PLANTILLA CORTA (F2-06) ─────────────────────

    // El motor ya no inventa el marcador cuando faltan jugadores: simula con los
    // disponibles (1–4) o resuelve una incomparecencia (0–20). El jugador recibe un
    // mensaje explicando lo que ha pasado con su equipo.
    void NotifyShortRoster(GameSimulator.GameResult result, GameData game)
    {
        bool isHome = game.home_team_id == _myTeam.id;
        int mine = isHome ? result.home_stats.Count : result.away_stats.Count;
        int rivalCount = isHome ? result.away_stats.Count : result.home_stats.Count;
        int rivalId = isHome ? game.away_team_id : game.home_team_id;
        string rival = DatabaseManager.Instance.GetTeamById(rivalId)?.name ?? "el rival";

        bool iForfeited = result.walkover && (isHome ? result.home_score == 0 : result.away_score == 0);
        bool rivalForfeited = result.walkover && (isHome ? result.away_score == 0 : result.home_score == 0);

        string title, body;
        if (iForfeited)
        {
            title = "Incomparecencia";
            body = $"No tenías ningún jugador sano disponible: el partido ante {rival} se pierde 0–20 por incomparecencia. Revisa la enfermería y la plantilla cuanto antes.";
        }
        else if (rivalForfeited)
        {
            title = "Incomparecencia del rival";
            body = $"{rival} no pudo presentar a ningún jugador sano: el partido se gana 20–0 por incomparecencia.";
        }
        else if (mine < 5)
        {
            title = "Plantilla incompleta";
            body = $"Has jugado ante {rival} con solo {mine} jugadores sanos. El resultado y el box score se han simulado con los disponibles; conviene reforzar la plantilla.";
        }
        else
        {
            title = "Plantilla incompleta del rival";
            body = $"{rival} ha jugado ante ti con solo {rivalCount} jugadores sanos. El resultado y el box score se han simulado con los disponibles.";
        }

        DatabaseManager.Instance.AddMessage(new MessageData
        {
            manager_id = _manager.id,
            sender_type = 0,
            sender_id = 0,
            title = title,
            body = body,
            game_day = _season.current_game_day,
            game_date = _season.current_date,
            created_at = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            date_sent = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            is_read = 0,
            category = NewsCategory.CLUB,
            news_type = NewsType.PLANTILLA_CORTA
        });
    }

    // ── G-LEAGUE: SIMULACIÓN DE PARTIDOS DE LA FILIAL ──────

    public static bool IsGLeagueGame(GameData g)
    {
        return g.game_type == GLeagueScheduleGenerator.TYPE_REGULAR
            || g.game_type == GLeaguePostSeason.TYPE_PLAYOFF;
    }

    /// <summary>
    /// Simula un partido G-League entre dos filiales (prospectos + asignados).
    /// No persiste en player_game_stats ni aplica lesiones/fatiga/records/moral:
    /// solo actualiza el GameData y acumula gleague_season_stats.
    /// </summary>
    void ProcessGLeagueGame(GameData game)
    {
        // Los partidos GL guardan los ids de filial codificados (+GAME_TEAM_ID_OFFSET)
        var homeTeam = DatabaseManager.Instance.GetGLeagueTeam(GLeagueHelper.DecodeGlTeamId(game.home_team_id));
        var awayTeam = DatabaseManager.Instance.GetGLeagueTeam(GLeagueHelper.DecodeGlTeamId(game.away_team_id));
        if (homeTeam == null || awayTeam == null)
        {
            Debug.LogError($"[Dashboard] Partido G-League {game.id} con filiales inexistentes ({game.home_team_id}/{game.away_team_id})");
            game.is_played = 1;
            DatabaseManager.Instance.UpdateGame(game);
            return;
        }

        var prospects = DatabaseManager.Instance.GetAllGLeaguePlayers();
        var assignedByNba = DatabaseManager.Instance.GetGLeagueAssignedByTeam();

        var homePlayers = GLeagueHelper.BuildAffiliateLineup(homeTeam, prospects, assignedByNba);
        var awayPlayers = GLeagueHelper.BuildAffiliateLineup(awayTeam, prospects, assignedByNba);

        var myAffiliate = _myTeam != null ? DatabaseManager.Instance.GetGLeagueTeamByNbaTeam(_myTeam.id) : null;
        bool isMyHomeGame = myAffiliate != null && homeTeam.id == myAffiliate.id;

        var result = GameSimulator.SimulateGame(game, homePlayers, awayPlayers, 50, 50, persistToDb:false, glLeague:true);
        DatabaseManager.Instance.UpdateGame(game);

        // Solo acumular stats de liga regular en gleague_season_stats;
        // los playoffs no se incluyen en las estadísticas de la pestaña.
        if (game.game_type == GLeagueScheduleGenerator.TYPE_REGULAR)
        {
            int seasonId = _season?.id ?? 0;
            foreach (var ps in result.home_stats)
            {
                if (ps.minutes <= 0) continue;
                var (pts, reb, ast, stl, blk, tov) =
                    GLeagueHelper.ClampLine(ps.points, ps.oreb + ps.dreb, ps.assists, ps.steals, ps.blocks, ps.turnovers);
                int missedFg = Mathf.Clamp(ps.fga - ps.fgm, 0, 30);
                int missedFt = Mathf.Clamp(ps.fta - ps.ftm, 0, 10);
                int pf = Mathf.Clamp(ps.pf, 0, 6);
                int clampedRating = pts + reb + ast + stl + blk - missedFg - missedFt - tov - pf;
                DatabaseManager.Instance.AddGLeagueGameStat(ps.player_id, seasonId,
                    pts, reb, ast, stl, blk, tov, clampedRating, homeTeam.id);
            }
            foreach (var ps in result.away_stats)
            {
                if (ps.minutes <= 0) continue;
                var (pts, reb, ast, stl, blk, tov) =
                    GLeagueHelper.ClampLine(ps.points, ps.oreb + ps.dreb, ps.assists, ps.steals, ps.blocks, ps.turnovers);
                int missedFg = Mathf.Clamp(ps.fga - ps.fgm, 0, 30);
                int missedFt = Mathf.Clamp(ps.fta - ps.ftm, 0, 10);
                int pf = Mathf.Clamp(ps.pf, 0, 6);
                int clampedRating = pts + reb + ast + stl + blk - missedFg - missedFt - tov - pf;
                DatabaseManager.Instance.AddGLeagueGameStat(ps.player_id, seasonId,
                    pts, reb, ast, stl, blk, tov, clampedRating, awayTeam.id);
            }
        }
    }

    void HandleDayError(string step, string modalTitle, string modalBody, System.Exception ex, bool fastSim)
    {
        Debug.LogError($"[GameDay] {step}, revertido: {ex.Message}\n{ex.StackTrace}");
        if (fastSim)
        {
            _fastSimAborted = true;
            // Guardar el motivo: al terminar el bucle se muestra al jugador (antes
            // el error solo quedaba en la consola y la simulación se detenía en silencio).
            _fastSimErrorMessage = $"{step}: {ex.Message}";
            _fastSimAbortEmptySlots.Clear();
            _fastSimAbortInjured.Clear();
        }
        else
        {
            HideLoading();
            ShowOfferResultModal(modalTitle, modalBody + "\n\n" + ex.Message, -1);
        }
    }

}
