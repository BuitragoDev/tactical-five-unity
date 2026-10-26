using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class DatabaseManager
{
    /// <summary>Crea (si falta) la edición de la NBA Cup de la temporada y sus grupos.</summary>
    public TournamentEditionData EnsureCupEdition(SeasonData season, List<TeamData> teams)
    {
        if (!EnsureDb() || season == null) return null;

        var existing = _db.Table<TournamentEditionData>()
            .Where(e => e.manager_id == season.manager_id && e.season_id == season.id)
            .FirstOrDefault();
        if (existing != null) return existing;

        var edition = new TournamentEditionData
        {
            season_id = season.id,
            manager_id = season.manager_id,
            year_start = season.year_start,
            status = "group",
            champion_team_id = 0,
            mvp_player_id = 0,
            created_at = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };
        _db.Insert(edition);

        if (teams != null)
        {
            foreach (var team in teams)
            {
                _db.Insert(new TournamentGroupData
                {
                    edition_id = edition.id,
                    group_name = CupHelper.GroupName(team),
                    conference = team.conference,
                    team_id = team.id
                });
            }
        }
        return edition;
    }

    public TournamentEditionData GetCupEdition(int seasonId)
        => _db.Table<TournamentEditionData>()
              .Where(e => e.season_id == seasonId)
              .FirstOrDefault();

    public void UpdateCupEdition(TournamentEditionData edition)
    {
        if (edition != null) _db.Update(edition);
    }

    public List<TournamentGroupData> GetCupGroups(int editionId)
        => _db.Table<TournamentGroupData>()
              .Where(g => g.edition_id == editionId)
              .OrderBy(g => g.group_name)
              .ToList();

    public List<TournamentStandingData> GetCupStandings(int editionId)
        => _db.Table<TournamentStandingData>()
              .Where(s => s.edition_id == editionId)
              .OrderBy(s => s.group_name)
              .ThenBy(s => s.rank)
              .ToList();

    public void ReplaceCupStandings(int editionId, List<TournamentStandingData> rows)
    {
        _db.Execute("DELETE FROM tournament_standings WHERE edition_id = ?", editionId);
        foreach (var row in rows ?? new List<TournamentStandingData>())
        {
            row.id = 0;
            row.edition_id = editionId;
            _db.Insert(row);
        }
    }

    /// <summary>Partidos de la Cup (grupo + eliminatoria) de una temporada.</summary>
    public List<GameData> GetCupGames(int seasonId, int managerId)
        => _db.Query<GameData>(
            "SELECT * FROM games WHERE season_id = ? AND manager_id = ? " +
            "AND (series_label LIKE 'cup-%') ORDER BY game_day, id",
            seasonId, managerId);

    /// <summary>Inserta partidos de eliminatoria de la Cup sin anidar transacción.</summary>
    public bool SaveCupGames(List<GameData> games)
    {
        if (games == null || games.Count == 0) return true;
        bool own = !_db.IsInTransaction;
        if (own) _db.BeginTransaction();
        try
        {
            foreach (var game in games)
                _db.Insert(game);
            if (own) _db.Commit();
            return true;
        }
        catch (Exception ex)
        {
            if (own) _db.Rollback();
            Debug.LogError($"[Cup] Error guardando partidos: {ex.Message}");
            return false;
        }
    }
}
