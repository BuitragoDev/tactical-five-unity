using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Snapshot del día que consumen los detectores de hilos narrativos. Se construye con
/// una sola consulta agregada de estadísticas (además de roster y calendario) y solo
/// para el equipo del usuario, de modo que añadir detectores no añade consultas.
/// Es un objeto de datos plano: los detectores son puros y testeables sin DB.
/// </summary>
public class StorylineContext
{
    public ManagerData manager;
    public TeamData myTeam;
    public SeasonData season;
    public int gameDay;
    public string gameDate;

    /// <summary>Partidos de liga regular jugados por mi equipo (orden cronológico).</summary>
    public List<GameData> MyPlayedGames = new();
    public int GamesPlayed;
    /// <summary>Racha actual (positiva = victorias seguidas).</summary>
    public int Streak;

    public List<PlayerData> Roster = new();
    /// <summary>Últimos partidos de cada jugador (más reciente primero, solo con minutos).</summary>
    public Dictionary<int, List<PlayerGameStats>> RecentByPlayer = new();
    /// <summary>Puntos por partido de la temporada por jugador (base de los umbrales relativos).</summary>
    public Dictionary<int, float> SeasonPpg = new();

    /// <summary>Posición en la conferencia (1 = primero; 0 = sin dato).</summary>
    public int ConferenceRank;
    /// <summary>Partidos de liga regular que quedan.</summary>
    public int GamesRemaining;

    public const int RECENT_GAMES = 5;

    public static StorylineContext Build(ManagerData manager, TeamData myTeam, SeasonData season, int gameDay, string gameDate)
    {
        if (manager == null || myTeam == null || season == null) return null;

        var ctx = new StorylineContext
        {
            manager = manager,
            myTeam = myTeam,
            season = season,
            gameDay = gameDay,
            gameDate = gameDate
        };

        // Solo los partidos de MI equipo (GetSeasonGames devuelve los de toda la liga).
        ctx.MyPlayedGames = StorylineService.RegularGamesOf(
            DatabaseManager.Instance.GetSeasonGames(manager.id, season.id), myTeam.id);
        ctx.GamesPlayed = ctx.MyPlayedGames.Count;
        ctx.Streak = StorylineService.CurrentStreak(ctx.MyPlayedGames, myTeam.id);

        ctx.GamesRemaining = Mathf.Max(0, 82 - ctx.GamesPlayed);
        ctx.ConferenceRank = ObjectiveHelper.GetConferenceRank(
            myTeam.id, myTeam.conference,
            DatabaseManager.Instance.GetAllTeams(),
            DatabaseManager.Instance.GetStandingsGames(manager.id));

        ctx.Roster = DatabaseManager.Instance.GetPlayersByTeam(myTeam.id);

        // Una consulta para los últimos partidos de toda la plantilla (los partidos de
        // la temporada se purgan en el rollover, así que todas las filas son actuales).
        var rows = DatabaseManager.Instance.Db.Query<PlayerGameStats>(
            "SELECT * FROM player_game_stats WHERE team_id = ? AND minutes > 0 ORDER BY player_id, game_id DESC",
            myTeam.id);
        var totalPoints = new Dictionary<int, int>();
        var totalGames = new Dictionary<int, int>();
        foreach (var r in rows)
        {
            if (!ctx.RecentByPlayer.TryGetValue(r.player_id, out var list))
            {
                list = new List<PlayerGameStats>();
                ctx.RecentByPlayer[r.player_id] = list;
            }
            if (list.Count < RECENT_GAMES) list.Add(r);

            // Totales de la temporada para el umbral relativo de sequía (sin consulta extra:
            // `rows` ya trae TODOS los partidos con minutos de la plantilla esta temporada).
            totalPoints.TryGetValue(r.player_id, out int pAcc);
            totalGames.TryGetValue(r.player_id, out int gAcc);
            totalPoints[r.player_id] = pAcc + r.points;
            totalGames[r.player_id] = gAcc + 1;
        }

        foreach (var kv in totalGames)
            if (kv.Value > 0)
                ctx.SeasonPpg[kv.Key] = (float)totalPoints[kv.Key] / kv.Value;

        return ctx;
    }

    public PlayerData FindPlayer(int id) => Roster.FirstOrDefault(p => p.id == id);
}
