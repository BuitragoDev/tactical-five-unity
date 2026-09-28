using System.Collections.Generic;
using System.Linq;

public partial class DatabaseManager
{
    // ── HILOS NARRATIVOS (storylines) ─────────────────────

    /// <summary>Hilos narrativos abiertos del equipo.</summary>
    public List<StorylineData> GetActiveStorylines(int managerId, int teamId)
    {
        if (!EnsureDb()) return new List<StorylineData>();
        return _db.Table<StorylineData>()
                  .Where(s => s.manager_id == managerId && s.team_id == teamId && s.resolved == 0)
                  .OrderBy(s => s.id)
                  .ToList();
    }

    /// <summary>Hilo abierto de un tipo concreto (o null).</summary>
    public StorylineData GetActiveStoryline(int managerId, int teamId, string type)
    {
        if (!EnsureDb()) return null;
        return _db.Table<StorylineData>()
                  .Where(s => s.manager_id == managerId && s.team_id == teamId
                           && s.resolved == 0 && s.type == type)
                  .OrderByDescending(s => s.id)
                  .FirstOrDefault();
    }

    /// <summary>Hilos abiertos cuyo protagonista es el jugador indicado (en cualquier equipo).</summary>
    public List<StorylineData> GetActiveStorylinesBySubject(int playerId)
    {
        if (!EnsureDb() || playerId <= 0) return new List<StorylineData>();
        return _db.Table<StorylineData>()
                  .Where(s => s.resolved == 0 && s.subject_player_id == playerId)
                  .ToList();
    }

    /// <summary>Hilo abierto de un tipo para un protagonista concreto (hilos por jugador).</summary>
    public StorylineData GetActiveStoryline(int managerId, int teamId, string type, int subjectPlayerId)
    {
        if (!EnsureDb()) return null;
        return _db.Table<StorylineData>()
                  .Where(s => s.manager_id == managerId && s.team_id == teamId
                           && s.resolved == 0 && s.type == type
                           && s.subject_player_id == subjectPlayerId)
                  .OrderByDescending(s => s.id)
                  .FirstOrDefault();
    }

    /// <summary>Último hilo de un tipo (abierto o cerrado), para el cooldown de reapertura.</summary>
    public StorylineData GetLastStoryline(int managerId, int teamId, string type)
    {
        if (!EnsureDb()) return null;
        return _db.Table<StorylineData>()
                  .Where(s => s.manager_id == managerId && s.team_id == teamId && s.type == type)
                  .OrderByDescending(s => s.id)
                  .FirstOrDefault();
    }

    /// <summary>Último hilo de un tipo para un protagonista (cooldown por jugador).</summary>
    public StorylineData GetLastStoryline(int managerId, int teamId, string type, int subjectPlayerId)
    {
        if (!EnsureDb()) return null;
        return _db.Table<StorylineData>()
                  .Where(s => s.manager_id == managerId && s.team_id == teamId && s.type == type
                           && s.subject_player_id == subjectPlayerId)
                  .OrderByDescending(s => s.id)
                  .FirstOrDefault();
    }

    /// <summary>Histórico de hilos (más recientes primero) para la línea temporal.</summary>
    public List<StorylineData> GetStorylineHistory(int managerId, int teamId, int limit = 60)
    {
        if (!EnsureDb()) return new List<StorylineData>();
        return _db.Table<StorylineData>()
                  .Where(s => s.manager_id == managerId && s.team_id == teamId)
                  .OrderByDescending(s => s.started_game_day)
                  .ThenByDescending(s => s.id)
                  .Take(limit)
                  .ToList();
    }

    public void InsertStoryline(StorylineData storyline)
    {
        if (!EnsureDb()) return;
        _db.Insert(storyline);
    }

    public void UpdateStoryline(StorylineData storyline)
    {
        if (!EnsureDb()) return;
        _db.Update(storyline);
    }

    /// <summary>Cierra todos los hilos abiertos (p. ej. al acabar la temporada).</summary>
    public void AbandonActiveStorylines(int managerId, string resolution = StorylineResolution.Abandoned)
    {
        if (!EnsureDb()) return;
        foreach (var s in _db.Table<StorylineData>().Where(x => x.manager_id == managerId && x.resolved == 0).ToList())
        {
            s.resolved = 1;
            s.resolution = resolution;
            _db.Update(s);
        }
    }
}
