using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// NBA Cup (F5-04): fase de grupos con los partidos de división que ya existen en
/// la temporada regular (se cuentan una sola vez) y eliminatoria adicional.
/// No toca `seasons.phase`.
/// </summary>
public static class CupHelper
{
    public const string GroupLabelPrefix = "cup-group-";
    public const string PlayoffGameType = "cup_playoff";
    public const string QfPrefix = "cup-qf-";
    public const string SfPrefix = "cup-sf-";
    public const string FinalLabel = "cup-final";

    /// <summary>Noches de la fase de grupos (1-based, igual que GameData.game_day).</summary>
    public static List<int> GroupPlayDays(SeasonData season)
    {
        var start = new DateTime(season.year_start, 10, 22);
        int Day(int month, int day) => (int)(new DateTime(season.year_start, month, day) - start).TotalDays + 1;
        return new List<int>
        {
            Day(10, 30), Day(11, 6), Day(11, 13), Day(11, 20),
            Day(11, 24), Day(11, 25), Day(11, 27)
        };
    }

    public static int QuarterfinalDay(SeasonData season) => CupDay(season, 12, 4);
    public static int SemifinalDay(SeasonData season) => CupDay(season, 12, 8);
    public static int FinalDay(SeasonData season) => CupDay(season, 12, 11);

    public static HashSet<int> BlockedRegularDays(SeasonData season)
    {
        var days = new HashSet<int>(GroupPlayDays(season))
        {
            QuarterfinalDay(season),
            SemifinalDay(season),
            FinalDay(season)
        };
        return days;
    }

    static int CupDay(SeasonData season, int month, int day)
    {
        var start = new DateTime(season.year_start, 10, 22);
        return (int)(new DateTime(season.year_start, month, day) - start).TotalDays + 1;
    }

    /// <summary>Grupo de la Cup: una división (5 equipos).</summary>
    public static string GroupName(TeamData team)
        => team == null ? "" : $"{team.conference} · {team.division}";

    /// <summary>
    /// 10 cruces de una división de 5 equipos. Cada equipo juega 4 partidos y
    /// exactamente 2 en casa (orientación cíclica de K5).
    /// </summary>
    public static IReadOnlyList<(int homeIndex, int awayIndex)> GroupPairings()
        => new[]
        {
            (0, 2), (0, 1),
            (1, 2), (1, 3),
            (2, 3), (2, 4),
            (3, 0), (3, 4),
            (4, 0), (4, 1),
        };

    /// <summary>
    /// Marca en el calendario regular 2 partidos por pareja divisional (uno por
    /// orientación) para que cada equipo tenga 4 partidos de grupo, 2 en casa.
    /// Devuelve cuántos partidos se marcaron.
    /// </summary>
    public static int MarkGroupGames(List<GameData> games, List<TeamData> teams)
    {
        if (games == null || teams == null) return 0;
        int marked = 0;

        foreach (var division in teams.GroupBy(t => new { t.conference, t.division }))
        {
            var clubs = division.OrderBy(t => t.id).ToList();
            if (clubs.Count != 5)
            {
                Debug.LogWarning($"[Cup] Grupo {division.Key.conference}/{division.Key.division} " +
                                 $"tiene {clubs.Count} equipos; se esperaban 5.");
                continue;
            }

            foreach (var (homeIndex, awayIndex) in GroupPairings())
            {
                int homeId = clubs[homeIndex].id;
                int awayId = clubs[awayIndex].id;
                var game = games
                    .Where(g => g.game_type == "regular"
                             && g.home_team_id == homeId
                             && g.away_team_id == awayId
                             && string.IsNullOrEmpty(g.series_label))
                    .OrderBy(g => g.game_day)
                    .FirstOrDefault();
                if (game == null)
                {
                    Debug.LogWarning($"[Cup] No hay partido {homeId}-{awayId} para marcar como grupo.");
                    continue;
                }
                game.series_label = GroupLabelPrefix + GroupName(clubs[homeIndex]);
                marked++;
            }
        }
        return marked;
    }

    /// <summary>Clasificación pura de la fase de grupos (solo partidos jugados).</summary>
    public static List<TournamentStandingData> ComputeStandings(
        List<TeamData> teams, List<GameData> games)
    {
        var rows = new Dictionary<int, TournamentStandingData>();
        foreach (var team in teams ?? new List<TeamData>())
        {
            rows[team.id] = new TournamentStandingData
            {
                group_name = GroupName(team),
                conference = team.conference,
                team_id = team.id
            };
        }

        foreach (var game in games ?? new List<GameData>())
        {
            if (game.is_played != 1 || !(game.series_label ?? "").StartsWith(GroupLabelPrefix))
                continue;
            if (!rows.TryGetValue(game.home_team_id, out var home)
                || !rows.TryGetValue(game.away_team_id, out var away))
                continue;

            home.points_for += game.home_score;
            home.points_against += game.away_score;
            away.points_for += game.away_score;
            away.points_against += game.home_score;
            if (game.home_score > game.away_score)
            {
                home.wins++;
                away.losses++;
            }
            else
            {
                away.wins++;
                home.losses++;
            }
        }

        foreach (var row in rows.Values)
            row.point_diff = row.points_for - row.points_against;

        var result = new List<TournamentStandingData>();
        foreach (var group in rows.Values.GroupBy(r => r.group_name).OrderBy(g => g.Key))
        {
            int rank = 1;
            foreach (var row in group
                .OrderByDescending(r => r.wins)
                .ThenByDescending(r => r.point_diff)
                .ThenByDescending(r => r.points_for)
                .ThenBy(r => r.team_id))
            {
                row.rank = rank++;
                result.Add(row);
            }
        }
        return result;
    }

    /// <summary>6 campeones de grupo + 1 wildcard por conferencia, ordenados por seed.</summary>
    public static List<int> QualifiedTeamIds(List<TournamentStandingData> standings)
    {
        if (standings == null) return new List<int>();
        var winners = standings.Where(s => s.rank == 1).ToList();
        var qualified = new List<TournamentStandingData>(winners);

        foreach (var conference in new[] { "East", "West" })
        {
            var wildcard = standings
                .Where(s => s.rank == 2 && s.conference == conference)
                .OrderByDescending(s => s.wins)
                .ThenByDescending(s => s.point_diff)
                .ThenByDescending(s => s.points_for)
                .ThenBy(s => s.team_id)
                .FirstOrDefault();
            if (wildcard != null) qualified.Add(wildcard);
        }

        return qualified
            .OrderByDescending(s => s.wins)
            .ThenByDescending(s => s.point_diff)
            .ThenByDescending(s => s.points_for)
            .ThenBy(s => s.team_id)
            .Select(s => s.team_id)
            .Take(8)
            .ToList();
    }

    /// <summary>
    /// Recalcula standings y avanza la eliminatoria. Devuelve cuántos partidos
    /// nuevos creó. Debe llamarse tras simular el día, dentro de la transacción.
    /// </summary>
    public static int AdvanceIfNeeded(SeasonData season, List<TeamData> teams)
    {
        if (season == null || teams == null || teams.Count == 0) return 0;
        var db = DatabaseManager.Instance;
        var edition = db.EnsureCupEdition(season, teams);
        if (edition == null || edition.status == "finished") return 0;

        var games = db.GetCupGames(season.id, season.manager_id);
        var groupGames = games.Where(g => (g.series_label ?? "").StartsWith(GroupLabelPrefix)).ToList();
        var standings = ComputeStandings(teams, groupGames);
        db.ReplaceCupStandings(edition.id, standings);

        if (edition.status == "group")
        {
            if (groupGames.Count < 60 || groupGames.Any(g => g.is_played != 1)) return 0;
            var seeds = QualifiedTeamIds(standings);
            if (seeds.Count < 8) return 0;

            var qf = new List<(int home, int away, string label)>
            {
                (seeds[0], seeds[7], QfPrefix + "1"),
                (seeds[1], seeds[6], QfPrefix + "2"),
                (seeds[2], seeds[5], QfPrefix + "3"),
                (seeds[3], seeds[4], QfPrefix + "4"),
            };
            int minDay = QuarterfinalDay(season);
            int created = CreateGames(season, games, qf, minDay);
            if (created == 0) return 0;

            edition.status = "knockout";
            db.UpdateCupEdition(edition);
            return created;
        }

        if (edition.status == "knockout")
        {
            var qfGames = games.Where(g => (g.series_label ?? "").StartsWith(QfPrefix))
                               .OrderBy(g => g.series_label).ToList();
            var sfGames = games.Where(g => (g.series_label ?? "").StartsWith(SfPrefix))
                               .OrderBy(g => g.series_label).ToList();
            var finalGames = games.Where(g => g.series_label == FinalLabel).ToList();

            if (qfGames.Count == 4 && qfGames.All(g => g.is_played == 1) && sfGames.Count == 0)
            {
                var winners = qfGames.Select(Winner).ToList();
                var sf = new List<(int home, int away, string label)>
                {
                    (winners[0], winners[3], SfPrefix + "1"),
                    (winners[1], winners[2], SfPrefix + "2"),
                };
                int minDay = SemifinalDay(season);
                return CreateGames(season, games, sf, minDay);
            }

            if (sfGames.Count == 2 && sfGames.All(g => g.is_played == 1) && finalGames.Count == 0)
            {
                var winners = sfGames.Select(Winner).ToList();
                int minDay = FinalDay(season);
                return CreateGames(season, games,
                    new List<(int home, int away, string label)> { (winners[0], winners[1], FinalLabel) },
                    minDay);
            }

            var final = finalGames.OrderBy(g => g.game_day).FirstOrDefault();
            if (final != null && final.is_played == 1)
            {
                int champion = Winner(final);
                var mvp = db.Db.Table<PlayerGameStats>()
                    .Where(s => s.game_id == final.id)
                    .OrderByDescending(s => s.rating)
                    .FirstOrDefault();
                edition.champion_team_id = champion;
                edition.mvp_player_id = mvp?.player_id ?? 0;
                edition.status = "finished";
                db.UpdateCupEdition(edition);
                AddChampionMessage(season, champion, mvp?.player_id ?? 0);
            }
        }
        return 0;
    }

    static int Winner(GameData game)
        => game.home_score >= game.away_score ? game.home_team_id : game.away_team_id;

    static int CreateGames(SeasonData season, List<GameData> existing,
        List<(int home, int away, string label)> matchups, int minDay)
    {
        var used = new HashSet<(int team, int day)>();
        var dayCount = new Dictionary<int, int>();
        foreach (var g in existing)
        {
            if (g.game_day <= 0) continue;
            used.Add((g.home_team_id, g.game_day));
            used.Add((g.away_team_id, g.game_day));
            dayCount[g.game_day] = dayCount.GetValueOrDefault(g.game_day, 0) + 1;
        }

        var seasonStart = new DateTime(season.year_start, 10, 22);
        var seasonEnd = new DateTime(season.year_end, 6, 30);
        int totalDays = (int)(seasonEnd - seasonStart).TotalDays + 1;
        var created = new List<GameData>();

        foreach (var (home, away, label) in matchups)
        {
            int chosen = -1;
            for (int day = Math.Max(1, minDay); day <= totalDays; day++)
            {
                var date = seasonStart.AddDays(day - 1);
                if (date.Month == 2 && date.Day >= 8 && date.Day <= 14) continue;
                if (used.Contains((home, day)) || used.Contains((away, day))) continue;
                if (dayCount.GetValueOrDefault(day, 0) >= 15) continue;
                chosen = day;
                break;
            }
            if (chosen < 0)
            {
                Debug.LogWarning($"[Cup] No hay día para {label}: {home} vs {away}.");
                return 0;
            }

            created.Add(new GameData
            {
                season_id = season.id,
                manager_id = season.manager_id,
                game_day = chosen,
                game_date = seasonStart.AddDays(chosen - 1).ToString("yyyy-MM-dd"),
                home_team_id = home,
                away_team_id = away,
                is_played = 0,
                game_type = PlayoffGameType,
                series_label = label,
                home_score = 0,
                away_score = 0,
                is_home = 0
            });
            used.Add((home, chosen));
            used.Add((away, chosen));
            dayCount[chosen] = dayCount.GetValueOrDefault(chosen, 0) + 1;
        }

        if (!DatabaseManager.Instance.SaveCupGames(created))
            return 0;
        Debug.Log($"[Cup] Creados {created.Count} partidos de eliminatoria.");
        return created.Count;
    }

    static void AddChampionMessage(SeasonData season, int championTeamId, int mvpPlayerId)
    {
        var team = DatabaseManager.Instance.GetTeamById(championTeamId);
        var mvp = mvpPlayerId > 0 ? DatabaseManager.Instance.GetPlayerById(mvpPlayerId) : null;
        string mvpText = mvp != null ? $" MVP de la final: {mvp.first_name} {mvp.last_name}." : "";
        string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        DatabaseManager.Instance.AddMessage(new MessageData
        {
            manager_id = season.manager_id,
            sender_type = 0,
            sender_id = 0,
            title = "NBA CUP: CAMPEÓN",
            body = $"{team?.name ?? "Un equipo"} gana la NBA Cup {season.year_start}-{season.year_end}.{mvpText}",
            game_day = season.current_game_day,
            game_date = season.current_date,
            created_at = now,
            date_sent = now,
            is_read = 0,
            category = NewsCategory.LIGA,
            news_type = NewsType.CUP_CHAMPION
        });
    }
}
