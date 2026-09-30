using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Orquesta el All-Star Weekend (E5): elige la convocatoria (12 por conferencia, 5+7),
/// simula los concursos de mates y triples y guarda los resultados. La parte de reglas
/// (selección, concursos) vive en helpers puros; esto solo las une con la base de datos.
/// </summary>
public static class AllStarWeekendService
{
    public const string EVENT_SLAM_DUNK = "slam_dunk";
    public const string EVENT_THREE_POINT = "three_point";

    /// <summary>
    /// Construye la convocatoria de una conferencia a partir de los jugadores disponibles
    /// (sin lesión ni G-League), usando sus medias de la temporada regular.
    /// </summary>
    public static List<AllStarCandidate> BuildConferenceRoster(int managerId, int seasonId,
                                                               string conference, List<PlayerData> allPlayers,
                                                               List<SeasonStatsAggregate> seasonStats)
    {
        var statsByPlayer = new Dictionary<int, (double avgRating, int games)>();
        if (seasonStats != null)
            foreach (var s in seasonStats)
                statsByPlayer[s.player_id] = (s.gp > 0 ? (double)s.total_rating / s.gp : 0, s.gp);

        var teamIds = DatabaseManager.Instance.GetAllTeams()
            .Where(t => t.conference == conference)
            .Select(t => t.id)
            .ToHashSet();

        var pool = allPlayers.Where(p => p != null && p.team_id > 0 && teamIds.Contains(p.team_id));

        var ranked = AllStarSelectionHelper.Rank(pool, p =>
            statsByPlayer.TryGetValue(p.id, out var v) ? v : (0, 0));

        return AllStarSelectionHelper.Select(ranked);
    }

    /// <summary>
    /// Corre los dos concursos y guarda el resultado. `field` sale de los convocados (24) y
    /// el reparto por atributo decide quién participa en cada uno. `rng` permite testearlo.
    /// </summary>
    public static void RunContests(int managerId, int seasonId, List<AllStarCandidate> eastField,
                                   List<AllStarCandidate> westField, List<PlayerData> allPlayers, Random rng)
    {
        var all = eastField.Concat(westField).ToList();
        if (all.Count > 1)
            all = all.GroupBy(c => c.player_id).Select(g => g.First()).ToList();

        var byId = allPlayers
            .Where(p => p != null)
            .GroupBy(p => p.id)
            .ToDictionary(g => g.Key, g => g.First());

        // Concurso de mates: los mejores por atletismo/velocidad/altura.
        var dunkField = AllStarSelectionHelper.ContestField(all, c =>
        {
            var p = byId.GetValueOrDefault(c.player_id);
            if (p == null) return 0;
            return p.athleticism * 2 + p.speed + Math.Clamp(p.height_cm - 190, 0, 25);
        }, SlamDunkContest.FIELD_SIZE);

        if (dunkField.Count >= 3)
        {
            var input = dunkField.Select(c =>
            {
                var p = byId.GetValueOrDefault(c.player_id);
                float skill = p != null
                    ? SlamDunkContest.Skill(p.athleticism, p.speed, p.height_cm)
                    : SlamDunkContest.Skill(c.overall, c.overall, 195);
                return (c.player_id, c.player_name, c.team_logo, skill);
            });
            var results = SlamDunkContest.Run(input, rng);
            SaveEvent(managerId, seasonId, EVENT_SLAM_DUNK, results);
        }

        // Concurso de triples: los mejores tiradores.
        var threeField = AllStarSelectionHelper.ContestField(all, c =>
        {
            var p = byId.GetValueOrDefault(c.player_id);
            return p != null ? p.three_point * 3 + p.shooting : c.overall;
        }, ThreePointContest.FIELD_SIZE);

        if (threeField.Count >= 3)
        {
            var input = threeField.Select(c =>
            {
                var p = byId.GetValueOrDefault(c.player_id);
                return (c.player_id, c.player_name, c.team_logo, p != null ? p.three_point : c.overall);
            });
            var results = ThreePointContest.Run(input, rng);
            SaveThreePointEvent(managerId, seasonId, results);
        }
    }

    static void SaveEvent(int managerId, int seasonId, string eventType, List<DunkResult> results)
    {
        if (results == null || results.Count == 0) return;
        var winner = results[0];
        var evt = new AllStarEventData
        {
            manager_id = managerId,
            season_id = seasonId,
            event_type = eventType,
            winner_player_id = winner.player_id,
            winner_name = winner.player_name,
            winner_team_logo = winner.team_logo,
            winner_score = winner.bestScore,
            created_at = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };
        var parts = results.Select(r => new AllStarEventParticipantData
        {
            player_id = r.player_id,
            player_name = r.player_name,
            team_logo = r.team_logo,
            rank = r.rank,
            score = r.bestScore,
            is_winner = r.rank == 1 ? 1 : 0
        }).ToList();

        DatabaseManager.Instance.SaveAllStarEvent(evt, parts);
    }

    static void SaveThreePointEvent(int managerId, int seasonId, List<ThreePointResult> results)
    {
        if (results == null || results.Count == 0) return;
        var winner = results[0];
        var evt = new AllStarEventData
        {
            manager_id = managerId,
            season_id = seasonId,
            event_type = EVENT_THREE_POINT,
            winner_player_id = winner.player_id,
            winner_name = winner.player_name,
            winner_team_logo = winner.team_logo,
            winner_score = winner.bestScore,
            created_at = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };
        var parts = results.Select(r => new AllStarEventParticipantData
        {
            player_id = r.player_id,
            player_name = r.player_name,
            team_logo = r.team_logo,
            rank = r.rank,
            score = r.bestScore,
            is_winner = r.champion ? 1 : 0
        }).ToList();

        DatabaseManager.Instance.SaveAllStarEvent(evt, parts);
    }

    /// <summary>
    /// Noticia del fin de semana con los campeones de los concursos. Devuelve true si había
    /// algún evento que narrar.
    /// </summary>
    public static bool AnnounceContests(int managerId, int seasonId, string seasonLabel)
    {
        var events = DatabaseManager.Instance.GetAllStarEvents(managerId)
            .Where(e => e.season_id == seasonId)
            .ToList();
        if (events.Count == 0) return false;

        var dunk = events.FirstOrDefault(e => e.event_type == EVENT_SLAM_DUNK);
        var three = events.FirstOrDefault(e => e.event_type == EVENT_THREE_POINT);

        string body = $"All-Star Weekend {seasonLabel}:\n";
        if (dunk != null) body += $"· Concurso de mates: {dunk.winner_name} ({dunk.winner_score} pts)\n";
        if (three != null) body += $"· Concurso de triples: {three.winner_name} ({three.winner_score} pts)\n";
        body += "\nLos concursos se disputaron en la noche del sábado, antes del partido de las estrellas.";

        DatabaseManager.Instance.AddMessage(new MessageData
        {
            manager_id = managerId,
            sender_type = 2,
            sender_id = 0,
            title = "All-Star Weekend: concursos",
            body = body,
            created_at = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            date_sent = DateTime.Now.ToString("yyyy-MM-dd"),
            is_read = 0,
            category = NewsCategory.LIGA,
            news_type = NewsType.ALLSTAR
        });
        return true;
    }
}
