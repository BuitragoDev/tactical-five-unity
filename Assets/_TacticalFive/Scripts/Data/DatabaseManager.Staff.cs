using UnityEngine;
using System.IO;
using System.Collections.Generic;
using SQLite;
using System;
using System.Linq;
using System.Globalization;

public partial class DatabaseManager
{
    // ── EMPLOYEE ────────────────────────────────────────

    public List<EmployeeData> GetEmployeesByTeam(int teamId)
    {
        return _db.Table<EmployeeData>()
                  .Where(e => e.team_id == teamId)
                  .ToList();
    }

    public List<EmployeeData> GetEmployeeCandidates()
    {
        return _db.Table<EmployeeData>()
                  .Where(e => e.team_id == 0)
                  .OrderBy(e => e.position)
                  .ToList();
    }

    public void InsertEmployee(EmployeeData emp)
    {
        _db.Insert(emp);
    }

    public void UpdateEmployee(EmployeeData emp)
    {
        _db.Update(emp);
    }

    public void DeleteEmployee(int id)
    {
        _db.Delete<EmployeeData>(id);
    }

    public void DeleteEmployeeCandidates()
    {
        _db.Execute("DELETE FROM employees WHERE team_id = 0");
    }

    // ── LOANS ────────────────────────────────────────

    public List<LoanData> GetLoansByTeam(int teamId)
    {
        return _db.Table<LoanData>()
                  .Where(l => l.team_id == teamId)
                  .OrderBy(l => l.slot)
                  .ToList();
    }

    public LoanData GetLoanBySlot(int teamId, int slot)
    {
        return _db.Table<LoanData>()
                  .FirstOrDefault(l => l.team_id == teamId && l.slot == slot);
    }

    public void InsertLoan(LoanData loan)
    {
        _db.Insert(loan);
    }

    public void UpdateLoan(LoanData loan)
    {
        _db.Update(loan);
    }

    public void DeleteLoan(int id)
    {
        _db.Delete<LoanData>(id);
    }

    // ── SCOUTS ────────────────────────────────────────

    public List<ScoutData> GetScoutsByTeam(int teamId)
    {
        return _db.Table<ScoutData>()
                  .Where(s => s.team_id == teamId)
                  .OrderBy(s => s.slot)
                  .ToList();
    }

    public ScoutData GetScoutBySlot(int teamId, int slot)
    {
        return _db.Table<ScoutData>()
                  .FirstOrDefault(s => s.team_id == teamId && s.slot == slot);
    }

    public void InsertScout(ScoutData scout)
    {
        _db.Insert(scout);
    }

    public void UpdateScout(ScoutData scout)
    {
        _db.Update(scout);
    }

    public void DeleteScout(int id)
    {
        _db.Delete<ScoutData>(id);
    }

    // ── SCOUTED PLAYERS (conocimiento persistente) ─────────

    public HashSet<int> GetScoutedPlayerIds(int teamId)
    {
        if (!EnsureDb()) return new HashSet<int>();
        return new HashSet<int>(
            _db.Table<ScoutedPlayerData>()
               .Where(s => s.team_id == teamId)
               .Select(s => s.player_id)
               .ToList());
    }

    public bool IsPlayerScouted(int teamId, int playerId)
    {
        if (!EnsureDb()) return false;
        return _db.Table<ScoutedPlayerData>()
                  .Where(s => s.team_id == teamId && s.player_id == playerId)
                  .Any();
    }

    public void MarkPlayerScouted(int teamId, int playerId, int scoutedDay)
    {
        if (!EnsureDb()) return;
        _db.Execute("INSERT OR IGNORE INTO scouted_players (team_id, player_id, scouted_day) VALUES (?, ?, ?)",
            teamId, playerId, scoutedDay);
    }

    public List<ScoutedPlayerData> GetScoutedPlayers(int teamId)
    {
        if (!EnsureDb()) return new List<ScoutedPlayerData>();
        return _db.Table<ScoutedPlayerData>()
                  .Where(s => s.team_id == teamId)
                  .OrderByDescending(s => s.scouted_day)
                  .ToList();
    }

}
