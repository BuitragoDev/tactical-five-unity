using UnityEngine;
using System.IO;
using System.Collections.Generic;
using SQLite;
using System;
using System.Linq;
using System.Globalization;

public partial class DatabaseManager
{
    public PlayerPersonalityData GetPlayerPersonality(int playerId)
    {
        if (!EnsureDb()) return null;
        return _db.Table<PlayerPersonalityData>()
                  .Where(p => p.player_id == playerId)
                  .FirstOrDefault();
    }

    public List<PlayerPersonalityData> GetTeamPersonalities(int teamId)
    {
        if (!EnsureDb()) return new List<PlayerPersonalityData>();
        return _db.Table<PlayerPersonalityData>()
                  .Where(p => p.team_id == teamId)
                  .ToList();
    }

    public void InsertOrUpdatePersonality(PlayerPersonalityData personality)
    {
        if (!EnsureDb()) return;
        var existing = GetPlayerPersonality(personality.player_id);
        if (existing != null)
        {
            personality.id = existing.id;
            _db.Update(personality);
        }
        else
        {
            _db.Insert(personality);
        }
    }

    public List<PlayerRelationshipData> GetTeamRelationships(int teamId)
    {
        if (!EnsureDb()) return new List<PlayerRelationshipData>();
        return _db.Table<PlayerRelationshipData>()
                  .Where(r => r.team_id == teamId)
                  .ToList();
    }

    public PlayerRelationshipData GetRelationship(int playerAId, int playerBId)
    {
        if (!EnsureDb()) return null;
        return _db.Table<PlayerRelationshipData>()
                  .Where(r => (r.player_a_id == playerAId && r.player_b_id == playerBId)
                           || (r.player_a_id == playerBId && r.player_b_id == playerAId))
                  .FirstOrDefault();
    }

    public void InsertOrUpdateRelationship(PlayerRelationshipData relationship)
    {
        if (!EnsureDb()) return;
        var existing = GetRelationship(relationship.player_a_id, relationship.player_b_id);
        if (existing != null)
        {
            relationship.id = existing.id;
            _db.Update(relationship);
        }
        else
        {
            _db.Insert(relationship);
        }
    }

    static readonly string[] PersonalityTypes = {
        "Líder", "Mentor", "Estrella", "Guerrero", "Tranquilo", "Intenso", "Profesional", "Novato"
    };

    static readonly (string t1, string t2, int compatMod)[][] PersonalityTraits = {
        new[] {("Carismático", "Motivador", 15), ("Comunicativo", "Exigente", 10)},        // Líder
        new[] {("Paciente", "Generoso", 12), ("Sabio", "Protector", 10)},                   // Mentor
        new[] {("Orgulloso", "Exigente", 0), ("Carismático", "Sensible", -5)},              // Estrella
        new[] {("Resiliente", "Competitivo", 10), ("Disciplinado", "Feroz", 8)},            // Guerrero
        new[] {("Respetuoso", "Estable", 8), ("Pacífico", "Constante", 5)},                 // Tranquilo
        new[] {("Apasionado", "Explosivo", 0), ("Competitivo", "Impulsivo", -8)},           // Intenso
        new[] {("Disciplinado", "Constante", 12), ("Responsable", "Puntual", 10)},          // Profesional
        new[] {("Entusiasta", "Respetuoso", 10), ("Hambriento", "Inquieto", 5)}             // Novato
    };

    public void SeedTeamPersonalities(int teamId, List<PlayerData> players)
    {
        if (!EnsureDb()) return;
        var rng = new System.Random();
        foreach (var p in players)
        {
            if (GetPlayerPersonality(p.id) != null) continue;
            int typeIdx = rng.Next(PersonalityTypes.Length);
            var traitPair = PersonalityTraits[typeIdx][rng.Next(2)];
            var data = new PlayerPersonalityData
            {
                player_id = p.id,
                team_id = teamId,
                personality_type = PersonalityTypes[typeIdx],
                trait_1 = traitPair.t1,
                trait_2 = traitPair.t2,
                compatibility_modifier = traitPair.compatMod
            };
            _db.Insert(data);
        }
    }

    public void SeedTeamRelationships(int teamId, List<PlayerData> players)
    {
        if (!EnsureDb()) return;
        var rng = new System.Random();
        for (int i = 0; i < players.Count; i++)
        {
            for (int j = i + 1; j < players.Count; j++)
            {
                if (GetRelationship(players[i].id, players[j].id) != null) continue;
                int compatMod = 0;
                var pA = GetPlayerPersonality(players[i].id);
                var pB = GetPlayerPersonality(players[j].id);
                if (pA != null && pB != null)
                    compatMod = (pA.compatibility_modifier + pB.compatibility_modifier) / 2;
                int bond = Mathf.Clamp(50 + compatMod + rng.Next(-12, 13), 1, 99);
                _db.Insert(new PlayerRelationshipData
                {
                    team_id = teamId,
                    player_a_id = players[i].id,
                    player_b_id = players[j].id,
                    bond = bond
                });
            }
        }
    }

    public void EnsureTeamRelationshipsSeeded(int teamId)
    {
        var players = GetPlayersByTeam(teamId);
        if (players.Count < 2) return;
        SeedTeamPersonalities(teamId, players);
        SeedTeamRelationships(teamId, players);
    }

    public void UpdateRelationshipsAfterGame(int teamId, int gameId, bool isWin, List<int> playedPlayerIds)
    {
        var rels = GetTeamRelationships(teamId);
        if (rels.Count == 0) return;
        var rng = new System.Random();
        foreach (var rel in rels)
        {
            bool aPlayed = playedPlayerIds.Contains(rel.player_a_id);
            bool bPlayed = playedPlayerIds.Contains(rel.player_b_id);
            int delta;
            if (aPlayed && bPlayed)
                delta = isWin ? rng.Next(1, 4) : rng.Next(0, 2);
            else if (aPlayed || bPlayed)
                delta = rng.Next(-1, 1);
            else
                delta = rng.Next(-2, 0);
            rel.bond = Mathf.Clamp(rel.bond + delta, 1, 99);
            _db.Update(rel);
        }
    }

    public List<LineupData> GetTeamLineup(int teamId)
    {
        if (!EnsureDb()) return new List<LineupData>();
        return _db.Table<LineupData>()
                  .Where(l => l.team_id == teamId)
                  .ToList();
    }

    public LineupData GetPlayerLineupSlot(int playerId)
    {
        if (!EnsureDb()) return null;
        return _db.Table<LineupData>()
                  .Where(l => l.player_id == playerId)
                  .FirstOrDefault();
    }

    public List<LineupData> GetStarters(int teamId)
    {
        return GetTeamLineup(teamId).Where(l => l.slot == 0).ToList();
    }

    public List<LineupData> GetBench(int teamId)
    {
        return GetTeamLineup(teamId).Where(l => l.slot == 1).ToList();
    }

    public List<LineupData> GetInactive(int teamId)
    {
        return GetTeamLineup(teamId).Where(l => l.slot == 2).ToList();
    }

    public void DeleteLineupEntry(int id)
    {
        if (!EnsureDb()) return;
        var entry = _db.Table<LineupData>().FirstOrDefault(l => l.id == id);
        if (entry != null)
            _db.Delete(entry);
    }

    public void SetPlayerSlot(int playerId, int teamId, int slot)
    {
        if (!EnsureDb()) return;
        var existing = GetPlayerLineupSlot(playerId);
        if (existing != null)
        {
            existing.slot = slot;
            existing.slot_index = -1;
            _db.Update(existing);
        }
        else
        {
            _db.Insert(new LineupData
            {
                player_id = playerId,
                team_id = teamId,
                slot = slot,
                slot_index = -1
            });
        }
    }

    public void SetPlayerSlot(int playerId, int teamId, int slot, int slotIndex)
    {
        if (!EnsureDb()) return;
        var existing = GetPlayerLineupSlot(playerId);
        if (existing != null)
        {
            existing.slot = slot;
            existing.slot_index = slotIndex;
            _db.Update(existing);
        }
        else
        {
            _db.Insert(new LineupData
            {
                player_id = playerId,
                team_id = teamId,
                slot = slot,
                slot_index = slotIndex
            });
        }
    }

    /// <summary>Guarda los minutos asignados en la pantalla ROTACIÓN para un jugador
    /// de la convocatoria. -1 = automático (heurística del simulador).</summary>
    public void SetLineupMinutes(int playerId, int minutes)
    {
        if (!EnsureDb()) return;
        var existing = GetPlayerLineupSlot(playerId);
        if (existing == null) return;
        existing.rotation_minutes = minutes;
        _db.Update(existing);
    }

    /// <summary>
    /// Asigna automáticamente los 240 minutos entre los convocados jugables (reparto de
    /// `RotationPlanner` con suelo de 8 min) y los persiste. Es lo que hace el botón AUTO
    /// de la pantalla ROTACIÓN y el botón del aviso de rotación del Dashboard.
    /// </summary>
    public void AutoAssignRotationMinutes(int teamId, List<PlayerData> players)
    {
        if (!EnsureDb() || players == null) return;

        var lineup = GetTeamLineup(teamId);
        if (lineup.Count == 0) return;

        var byId = players.ToDictionary(p => p.id);
        var entries = new List<(LineupData l, PlayerData p)>();
        foreach (var l in lineup)
        {
            if (l.slot != 0 && l.slot != 1) continue;
            if (!byId.TryGetValue(l.player_id, out var p) || p == null) continue;
            if (p.injury_days > 0 || p.g_league_assigned == 1) continue;
            entries.Add((l, p));
        }
        if (entries.Count == 0) return;

        var suggestions = entries.Select(e => GameSimulator.GetSuggestedMinutes(e.p, e.l.slot == 0)).ToList();
        var mins = RotationPlanner.AutoAssign(suggestions);

        _db.RunInTransaction(() =>
        {
            for (int i = 0; i < entries.Count && i < mins.Length; i++)
            {
                entries[i].l.rotation_minutes = mins[i];
                _db.Update(entries[i].l);
            }
        });
    }

    /// <summary>Número máximo de NO CONVOCADOS (slot 2) en la convocatoria.</summary>
    public const int INACTIVE_SLOTS = 6;

    /// <summary>Garantiza que la convocatoria (team_lineup) existe y está al día con la
    /// plantilla: crea la convocatoria automática si está vacía, elimina filas de
    /// jugadores que ya no están en el equipo y mete a los nuevos en NO CONVOCADOS.
    /// Devuelve la convocatoria resultante.</summary>
    public List<LineupData> EnsureLineupSeeded(int teamId, List<PlayerData> players)
    {
        if (!EnsureDb()) return new List<LineupData>();

        // Atómico: borrados + inserciones de la convocatoria no pueden quedar a medias.
        List<LineupData> result = new List<LineupData>();
        _db.RunInTransaction(() => { result = EnsureLineupSeededCore(teamId, players); });
        return result;
    }

    List<LineupData> EnsureLineupSeededCore(int teamId, List<PlayerData> players)
    {
        var existing = GetTeamLineup(teamId);
        if (existing.Count == 0)
        {
            var excluded = new HashSet<int>(players.Where(p => p.g_league_assigned == 1).Select(p => p.id));
            AutoSeedLineup(teamId, players, excluded);
        }
        else
        {
            var currentIds = new HashSet<int>(players.Select(p => p.id));
            foreach (var e in existing)
            {
                if (!currentIds.Contains(e.player_id))
                    DeleteLineupEntry(e.id);
            }

            existing = GetTeamLineup(teamId);
            var assigned = new HashSet<int>(existing.Select(l => l.player_id));
            int nextInactIdx = existing.Where(l => l.slot == 2)
                                       .Select(l => l.slot_index)
                                       .DefaultIfEmpty(-1).Max() + 1;
            foreach (var p in players)
            {
                if (assigned.Contains(p.id)) continue;
                if (nextInactIdx >= INACTIVE_SLOTS) break;
                SetPlayerSlot(p.id, teamId, 2, nextInactIdx);
                nextInactIdx++;
            }
        }

        return GetTeamLineup(teamId);
    }

    public void AutoSeedLineup(int teamId, List<PlayerData> players, HashSet<int> forceInactiveIds = null)
    {
        if (!EnsureDb()) return;
        if (players.Count == 0) return;

        // Atómico: borrar la convocatoria anterior e insertar la nueva no puede quedar a medias.
        _db.RunInTransaction(() => AutoSeedLineupCore(teamId, players, forceInactiveIds));
    }

    void AutoSeedLineupCore(int teamId, List<PlayerData> players, HashSet<int> forceInactiveIds)
    {
        // Remove any existing lineup for this team
        var existing = GetTeamLineup(teamId);
        foreach (var e in existing)
            _db.Delete(e);

        var assigned = new HashSet<int>();
        var forceInactive = forceInactiveIds ?? new HashSet<int>();

        var posOrder = new[] { "PG", "SG", "SF", "PF", "C" };
        var starters = new PlayerData[posOrder.Length];
        var used = new HashSet<int>();

        PlayerData[] available = players.Where(p => !forceInactive.Contains(p.id)).ToArray();

        // ── Phase 1: primary position assignment (greedy by OVR) ──
        // Each pass assigns the best available player whose PRIMARY position matches
        // the highest-OVR unfilled slot. This ensures natural starters get priority.
        for (int pass = 0; pass < posOrder.Length; pass++)
        {
            int bestPos = -1;
            int bestIdx = -1;
            int bestOvr = -1;

            for (int si = 0; si < posOrder.Length; si++)
            {
                if (starters[si] != null) continue;
                for (int pi = 0; pi < available.Length; pi++)
                {
                    var p = available[pi];
                    if (used.Contains(p.id)) continue;
                    if (p.position != posOrder[si]) continue;
                    if (p.overall > bestOvr)
                    {
                        bestOvr = p.overall;
                        bestPos = si;
                        bestIdx = pi;
                    }
                }
            }

            if (bestPos >= 0 && bestIdx >= 0)
            {
                starters[bestPos] = available[bestIdx];
                used.Add(available[bestIdx].id);
            }
        }

        // ── Phase 2: secondary position fill ──
        // For empty slots, try players whose SECONDARY position matches.
        for (int pass = 0; pass < posOrder.Length; pass++)
        {
            int bestPos = -1;
            int bestIdx = -1;
            int bestOvr = -1;

            for (int si = 0; si < posOrder.Length; si++)
            {
                if (starters[si] != null) continue;
                for (int pi = 0; pi < available.Length; pi++)
                {
                    var p = available[pi];
                    if (used.Contains(p.id)) continue;
                    if (p.secondary_position != posOrder[si]) continue;
                    if (p.overall > bestOvr)
                    {
                        bestOvr = p.overall;
                        bestPos = si;
                        bestIdx = pi;
                    }
                }
            }

            if (bestPos >= 0 && bestIdx >= 0)
            {
                starters[bestPos] = available[bestIdx];
                used.Add(available[bestIdx].id);
            }
        }

        // ── Phase 2b: swap pass — upgrade starters with better secondary-position bench players ──
        // If a bench player whose secondary matches a starter slot has higher OVR, swap them.
        // The displaced starter re-enters the pool and may be recaptured in later rounds.
        for (int round = 0; round < 3; round++)
        {
            bool swapped = false;
            for (int si = 0; si < posOrder.Length; si++)
            {
                if (starters[si] == null) continue;
                for (int pi = 0; pi < available.Length; pi++)
                {
                    var p = available[pi];
                    if (used.Contains(p.id)) continue;
                    if (p.secondary_position != posOrder[si]) continue;
                    if (p.overall > starters[si].overall)
                    {
                        used.Remove(starters[si].id);
                        starters[si] = p;
                        used.Add(p.id);
                        swapped = true;
                        break;
                    }
                }
            }
            if (!swapped) break;
        }

        // ── Phase 3: similar-position fallback ──
        // For slots still empty, try the best player from an adjacent/similar position.
        // PG↔SG, PF↔C, SF→SG (if ≤201cm) or SF→PF (if >201cm).
        for (int si = 0; si < posOrder.Length; si++)
        {
            if (starters[si] != null) continue;

            string targetPos = posOrder[si];
            string fallbackPos1 = null;
            string fallbackPos2 = null;
            int heightThreshold = 0;

            switch (targetPos)
            {
                case "PG": fallbackPos1 = "SG"; break;
                case "SG": fallbackPos1 = "PG"; break;
                case "SF": fallbackPos1 = "SG"; fallbackPos2 = "PF"; heightThreshold = 201; break;
                case "PF": fallbackPos1 = "C"; break;
                case "C":  fallbackPos1 = "PF"; break;
            }

            // Try primary=fallbackPos1 (primary first, then secondary, height-preferred for SF)
            var candidates = available
                .Where(p => !used.Contains(p.id) && (p.position == fallbackPos1 || p.secondary_position == fallbackPos1))
                .OrderByDescending(p => p.position == fallbackPos1 ? 1 : 0)
                .ThenByDescending(p => p.overall)
                .ToList();

            if (targetPos == "SF" && heightThreshold > 0)
            {
                // For SF, prefer SG ≤201cm or PF >201cm; don't enforce strictly,
                // just order them: exact-height match first, then any.
                var sgShort = candidates.Where(p => p.height_cm <= heightThreshold);
                var sgAny = candidates.Where(p => p.height_cm > heightThreshold);
                candidates = sgShort.Concat(sgAny).ToList();
            }

            if (candidates.Count > 0)
            {
                starters[si] = candidates[0];
                used.Add(candidates[0].id);
                continue;
            }

            // Try fallbackPos2 (for SF: the other wing position)
            if (fallbackPos2 != null)
            {
                var candidates2 = available
                    .Where(p => !used.Contains(p.id) && (p.position == fallbackPos2 || p.secondary_position == fallbackPos2))
                    .OrderByDescending(p => p.position == fallbackPos2 ? 1 : 0)
                    .ThenByDescending(p => p.overall)
                    .ToList();

                if (targetPos == "SF" && heightThreshold > 0)
                {
                    var pfTall = candidates2.Where(p => p.height_cm > heightThreshold);
                    var pfAny = candidates2.Where(p => p.height_cm <= heightThreshold);
                    candidates2 = pfTall.Concat(pfAny).ToList();
                }

                if (candidates2.Count > 0)
                {
                    starters[si] = candidates2[0];
                    used.Add(candidates2[0].id);
                }
            }
        }

        // ── Persist starters ──
        for (int si = 0; si < posOrder.Length; si++)
        {
            if (starters[si] == null) continue;
            _db.Insert(new LineupData
            {
                player_id = starters[si].id,
                team_id = teamId,
                slot = 0,
                slot_index = si
            });
            assigned.Add(starters[si].id);
        }

        // ── Bench: next best unassigned players (up to 12 total active) ──
        var remaining = players
            .Where(p => !assigned.Contains(p.id) && !forceInactive.Contains(p.id))
            .OrderByDescending(p => p.overall)
            .ToList();

        int maxActive = 12;
        int benchSlots = Mathf.Min(remaining.Count, maxActive - assigned.Count);
        for (int i = 0; i < benchSlots; i++)
        {
            _db.Insert(new LineupData
            {
                player_id = remaining[i].id,
                team_id = teamId,
                slot = 1,
                slot_index = i
            });
            assigned.Add(remaining[i].id);
        }

        // ── Inactive: forced-inactive first, then overflow ──
        int inactIdx = 0;
        const int maxInactive = INACTIVE_SLOTS;

        foreach (var p in players.Where(p => forceInactive.Contains(p.id)))
        {
            if (inactIdx >= maxInactive) break;
            _db.Insert(new LineupData
            {
                player_id = p.id,
                team_id = teamId,
                slot = 2,
                slot_index = inactIdx
            });
            inactIdx++;
        }

        foreach (var p in remaining.Skip(benchSlots))
        {
            if (inactIdx >= maxInactive) break;
            _db.Insert(new LineupData
            {
                player_id = p.id,
                team_id = teamId,
                slot = 2,
                slot_index = inactIdx
            });
            inactIdx++;
        }
    }

    /// <summary>
    /// AI decides whether to exercise a team option. The option year is the current upcoming season.
    /// Criteria: the player contributes positively (overall above threshold relative to salary/slot).
    /// </summary>
    public bool DecideTeamOption(PlayerData p)
    {
        // Always exercise if the player is decent and salary is reasonable
        if (p.overall < 65) return false;
        // Reject if the player is old and declining (age > 34 or overall < potential-10)
        if (p.age > 34 && p.overall < p.potential - 10) return false;
        // Accept otherwise
        return true;
    }

    /// <summary>
    /// True si el jugador es un agente libre reciente que jugó para `teamId`
    /// (conserva Bird rights / contexto de re-firma aunque ahora sea FA).
    /// </summary>
    public bool IsOwnRecentFA(PlayerData p, int teamId)
    {
        return p.team_id == 0 && p.last_team_id == teamId && p.seasons_with_team > 0;
    }

    /// <summary>
    /// AI decides whether the player exercises their player option (opts out to test FA market).
    /// Uses a weighted score: market value, happiness, loyalty, role, age, team success.
    /// </summary>
    public bool DecidePlayerOption(PlayerData p, float teamWinPct, LeagueSettingsData settings)
    {
        if (settings == null) return DecidePlayerOptionLegacy(p);

        long cap = settings.salary_cap;
        float score = 50f;

        // 1. Market: current salary vs estimated market value
        long marketSalary = EstimateMarketSalary(p.overall, cap);
        float salaryRatio = marketSalary > 0 ? (float)p.salary / marketSalary : 1f;
        if (salaryRatio >= 1.0f)      score += 20f;  // well paid → stay
        else if (salaryRatio < 0.70f) score -= 25f;  // underpaid → seek market

        // 2. Happiness + loyalty
        score += (p.morale - 50) * 0.5f;                    // morale: -25 to +25
        score += System.Math.Min(p.seasons_with_team, 5) * 4f;  // loyalty: +4/yr, max +20

        // 3. Role
        score += p.role switch
        {
            PlayerRole.Estrella => 15f,
            PlayerRole.Titular => 5f,
            PlayerRole.Banquillo => -5f,
            PlayerRole.UltimoRecurso => -15f,
            _ => 0f
        };

        // 4. Age
        if (p.age < 26 && p.potential >= p.overall + 3) score -= 15f;  // young + upside → bet
        else if (p.age > 33)                               score += 10f;  // veteran → security

        // 5. Team success
        if (teamWinPct >= 0.60f)      score += 15f;   // contender
        else if (teamWinPct <= 0.35f) score -= 15f;   // tanking

        // 6. Controlled randomness (±10)
        score += Rng.Next(-10, 11);

        return score >= 50f;
    }

    bool DecidePlayerOptionLegacy(PlayerData p)
    {
        if (p.overall >= 85) return false;
        if (p.age < 28 && p.overall >= 80 && p.potential >= 85) return false;
        return true;
    }

    /// <summary>
    /// Estimated market salary based on overall rating.
    /// Formula: (overall - 40) * 0.005 of the salary cap, clamped to [2%, 35%].
    /// </summary>
    public long EstimateMarketSalary(int overall, long cap)
    {
        float pct = (overall - 40) * 0.005f;
        pct = System.Math.Clamp(pct, 0.02f, 0.35f);
        return (long)(cap * pct);
    }

    void ApplyMentoring(List<PlayerData> allPlayers)
    {
        var byTeam = allPlayers
            .Where(p => p.is_rookie == 0 && p.team_id != 0)
            .GroupBy(p => p.team_id);

        foreach (var teamGroup in byTeam)
        {
            var youngPlayers = teamGroup.Where(p => p.age >= 19 && p.age <= 22).ToList();
            if (youngPlayers.Count == 0) continue;

            var veterans = teamGroup.Where(p => p.age >= 30 && p.iq >= 70 && p.morale >= 60)
                .OrderByDescending(p => p.iq).ToList();
            if (veterans.Count == 0) continue;

            var mentor = veterans[0];
            int bonus = mentor.iq >= 80 ? 2 : 1;

            var priorityAttrsByPos = new Dictionary<string, string[]>
            {
                { "PG", new[] { "passing", "dribbling", "speed", "iq", "three_point" } },
                { "SG", new[] { "shooting", "three_point", "speed", "dribbling", "steals" } },
                { "SF", new[] { "shooting", "defense", "athleticism", "speed", "rebounding" } },
                { "PF", new[] { "defense", "rebounding", "blocks", "athleticism" } },
                { "C",  new[] { "rebounding", "blocks", "defense", "iq", "athleticism" } }
            };

            foreach (var young in youngPlayers)
            {
                if (!priorityAttrsByPos.TryGetValue(young.position, out var attrs))
                    continue;

                string targetAttr = attrs[Rng.Next(0, attrs.Length)];
                int oldVal = GetAttr(young, targetAttr);
                int newVal = PlayerAttributeScale.ClampValid(oldVal + bonus);
                SetAttr(young, targetAttr, newVal);
                young.RecalculateOverall(); // el overall debe seguir a los atributos
                _db.Update(young);
            }
        }
    }

    static int GetAttr(PlayerData p, string attr) => attr switch
    {
        "speed" => p.speed,
        "shooting" => p.shooting,
        "three_point" => p.three_point,
        "passing" => p.passing,
        "dribbling" => p.dribbling,
        "defense" => p.defense,
        "rebounding" => p.rebounding,
        "athleticism" => p.athleticism,
        "iq" => p.iq,
        "steals" => p.steals,
        "blocks" => p.blocks,
        _ => 0
    };

    static void SetAttr(PlayerData p, string attr, int val)
    {
        switch (attr)
        {
            case "speed": p.speed = val; break;
            case "shooting": p.shooting = val; break;
            case "three_point": p.three_point = val; break;
            case "passing": p.passing = val; break;
            case "dribbling": p.dribbling = val; break;
            case "defense": p.defense = val; break;
            case "rebounding": p.rebounding = val; break;
            case "athleticism": p.athleticism = val; break;
            case "iq": p.iq = val; break;
            case "steals": p.steals = val; break;
            case "blocks": p.blocks = val; break;
        }
    }
}
