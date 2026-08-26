using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

// ══════════════════════════════════════════════════════
//  GLeagueHelper — reglas de asignación y desarrollo
//  (tests originales de la Propuesta D, conservados)
// ══════════════════════════════════════════════════════

public class GLeagueHelperTests
{
    static PlayerData P(int injuryDays = 0, int onIR = 0, int glAssigned = 0, int teamId = 1)
    {
        return new PlayerData
        {
            team_id = teamId,
            injury_days = injuryDays,
            is_on_ir = onIR,
            g_league_assigned = glAssigned
        };
    }

    // ── Elegibilidad two-way (TradeHelper) ────────────────

    [Test]
    public void IsEligibleForTwoWay_AgeThreshold()
    {
        Assert.That(TradeHelper.IsEligibleForTwoWay(new PlayerData { age = 23 }), Is.True);
        Assert.That(TradeHelper.IsEligibleForTwoWay(new PlayerData { age = 21 }), Is.True);
        Assert.That(TradeHelper.IsEligibleForTwoWay(new PlayerData { age = 24 }), Is.False);
    }

    [Test]
    public void IsEligibleForIR_MinDays()
    {
        Assert.That(TradeHelper.IsEligibleForIR(new PlayerData { injury_days = 90 }), Is.True);
        Assert.That(TradeHelper.IsEligibleForIR(new PlayerData { injury_days = 110 }), Is.True);
        Assert.That(TradeHelper.IsEligibleForIR(new PlayerData { injury_days = 89 }), Is.False);
    }

    // ── GLeagueHelper ─────────────────────────────────────

    [Test]
    public void CanAssign_OnlyHealthyNonIRNonGL()
    {
        Assert.That(GLeagueHelper.CanAssign(P()), Is.True);
        Assert.That(GLeagueHelper.CanAssign(P(injuryDays: 1)), Is.False);
        Assert.That(GLeagueHelper.CanAssign(P(onIR: 1)), Is.False);
        Assert.That(GLeagueHelper.CanAssign(P(glAssigned: 1)), Is.False);
        Assert.That(GLeagueHelper.CanAssign(P(teamId: 0)), Is.False);
        Assert.That(GLeagueHelper.CanAssign(null), Is.False);
    }

    [Test]
    public void CanRecall_OnlyAssigned()
    {
        Assert.That(GLeagueHelper.CanRecall(P(glAssigned: 1)), Is.True);
        Assert.That(GLeagueHelper.CanRecall(P()), Is.False);
        Assert.That(GLeagueHelper.CanRecall(null), Is.False);
    }

    [Test]
    public void HasEnoughActive_RequiresTwelveAfterAssignment()
    {
        var thirteen = new List<PlayerData>();
        for (int i = 0; i < 13; i++) thirteen.Add(P());
        Assert.That(GLeagueHelper.HasEnoughActive(thirteen), Is.True);

        var twelve = new List<PlayerData>();
        for (int i = 0; i < 12; i++) twelve.Add(P());
        Assert.That(GLeagueHelper.HasEnoughActive(twelve), Is.False);
    }

    [Test]
    public void HasEnoughActive_IgnoresInjuredIRAndGL()
    {
        var roster = new List<PlayerData>();
        for (int i = 0; i < 11; i++) roster.Add(P());
        roster.Add(P(injuryDays: 5));
        roster.Add(P(onIR: 1));
        roster.Add(P(glAssigned: 1));
        // Solo 11 activos: 11-1=10 < 12 → false
        Assert.That(GLeagueHelper.HasEnoughActive(roster), Is.False);
    }

    [Test]
    public void ProcessDevelopmentTick_RespectsPotentialAndRecalculatesOverall()
    {
        var p = new PlayerData
        {
            team_id = 1,
            g_league_assigned = 1,
            potential = 60,
            speed = 50, shooting = 50, three_point = 50, passing = 50,
            dribbling = 50, defense = 50, rebounding = 50, athleticism = 50,
            iq = 50, steals = 50, blocks = 50
        };

        for (int i = 0; i < 200; i++)
            GLeagueHelper.ProcessDevelopmentTick(p);

        Assert.That(p.speed, Is.LessThanOrEqualTo(p.potential));
        Assert.That(p.shooting, Is.LessThanOrEqualTo(p.potential));
        Assert.That(p.three_point, Is.LessThanOrEqualTo(p.potential));
        Assert.That(p.passing, Is.LessThanOrEqualTo(p.potential));
        Assert.That(p.dribbling, Is.LessThanOrEqualTo(p.potential));
        Assert.That(p.defense, Is.LessThanOrEqualTo(p.potential));
        Assert.That(p.rebounding, Is.LessThanOrEqualTo(p.potential));
        Assert.That(p.athleticism, Is.LessThanOrEqualTo(p.potential));
        Assert.That(p.iq, Is.LessThanOrEqualTo(p.potential));
        Assert.That(p.steals, Is.LessThanOrEqualTo(p.potential));
        Assert.That(p.blocks, Is.LessThanOrEqualTo(p.potential));

        // overall se mantiene consistente con la media de 11 atributos capada por potential
        Assert.That(p.overall, Is.EqualTo(System.Math.Min(p.potential, p.GetCalculatedAverage())));
    }

    [Test]
    public void ProcessDevelopmentTick_OnlyAppliesToAssignedPlayers()
    {
        var notAssigned = new PlayerData
        {
            team_id = 1,
            g_league_assigned = 0,
            potential = 60,
            speed = 50, shooting = 50, three_point = 50, passing = 50,
            dribbling = 50, defense = 50, rebounding = 50, athleticism = 50,
            iq = 50, steals = 50, blocks = 50
        };
        Assert.That(GLeagueHelper.ProcessDevelopmentTick(notAssigned), Is.False);
        Assert.That(notAssigned.speed, Is.EqualTo(50));
    }
}

// ══════════════════════════════════════════════════════
//  Liga completa: calendario / clasificación / postseason
// ══════════════════════════════════════════════════════

public class GLeagueScheduleGeneratorTests
{
    const int Offset = GLeagueHelper.GAME_TEAM_ID_OFFSET;

    static List<GLeagueTeamData> ThirtyTeams()
    {
        var teams = new List<GLeagueTeamData>();
        for (int i = 1; i <= 30; i++)
            teams.Add(new GLeagueTeamData
            {
                id = i,
                name = $"Filial {i}",
                conference = i <= 15 ? "East" : "West"
            });
        return teams;
    }

    // Días NBA (game_day 1-based): días alternos del 1 nov 2025 al 19 mar 2026
    static IEnumerable<int> NbaDays()
    {
        var start = new DateTime(2025, 10, 22);
        for (var d = new DateTime(2025, 11, 1); d <= new DateTime(2026, 3, 19); d = d.AddDays(2))
            yield return (int)(d - start).TotalDays + 1;
    }

    static SeasonData Season() => new SeasonData { id = 1, manager_id = 1, year_start = 2025, year_end = 2026 };

    // ── DoubleRoundRobin ─────────────────────────────────

    [Test]
    public void DoubleRoundRobin_15Teams_30RondasDe7Partidos()
    {
        var rounds = GLeagueScheduleGenerator.DoubleRoundRobin(
            ThirtyTeams().Where(t => t.conference == "East").ToList());

        Assert.That(rounds.Count, Is.EqualTo(30));
        foreach (var round in rounds)
        {
            Assert.That(round.Count, Is.EqualTo(7));
            var teamsInRound = round.SelectMany(m => new[] { m.home, m.away }).Distinct().ToList();
            Assert.That(teamsInRound.Count, Is.EqualTo(14)); // nadie repite en la jornada
        }
    }

    [Test]
    public void DoubleRoundRobin_CadaParejaSeEnfrentaDosVecesConLocaliaInvertida()
    {
        var teams = ThirtyTeams().Where(t => t.conference == "East").ToList();
        var rounds = GLeagueScheduleGenerator.DoubleRoundRobin(teams);
        var all = rounds.SelectMany(r => r).ToList();

        Assert.That(all.Count, Is.EqualTo(30 * 7));

        foreach (var pair in all.GroupBy(m => m.home < m.away ? (m.home, m.away) : (m.away, m.home)))
            Assert.That(pair.Count(), Is.EqualTo(2), $"Par {pair.Key} debe jugarse 2 veces");

        // Localía invertida entre vuelta y vuelta
        foreach (var pair in all.GroupBy(m => m.home < m.away ? (m.home, m.away) : (m.away, m.home)))
        {
            var homes = pair.Select(m => m.home).Distinct().ToList();
            Assert.That(homes.Count, Is.EqualTo(2), "Cada equipo debe ser local una vez");
        }
    }

    // ── BuildSchedule ────────────────────────────────────

    [Test]
    public void BuildSchedule_28PartidosPorEquipo()
    {
        var teams = ThirtyTeams();
        var games = GLeagueScheduleGenerator.BuildSchedule(Season(), teams, NbaDays());

        Assert.That(games.Count, Is.EqualTo(420));

        foreach (var team in teams)
        {
            int played = games.Count(g =>
                GLeagueHelper.DecodeGlTeamId(g.home_team_id) == team.id
                || GLeagueHelper.DecodeGlTeamId(g.away_team_id) == team.id);
            Assert.That(played, Is.EqualTo(GLeagueScheduleGenerator.GAMES_PER_TEAM),
                $"Filial {team.name} debe jugar 28 partidos");
        }
    }

    [Test]
    public void BuildSchedule_NingunEquipoJuegaDosVecesElMismoDia()
    {
        var teams = ThirtyTeams();
        var games = GLeagueScheduleGenerator.BuildSchedule(Season(), teams, NbaDays());

        var seen = new HashSet<(int team, int day)>();
        foreach (var g in games)
        {
            Assert.That(seen.Add((GLeagueHelper.DecodeGlTeamId(g.home_team_id), g.game_day)), Is.True);
            Assert.That(seen.Add((GLeagueHelper.DecodeGlTeamId(g.away_team_id), g.game_day)), Is.True);
        }
    }

    [Test]
    public void BuildSchedule_SoloDiasDisponibles_YFueraDeSemanaAllStar()
    {
        var allowed = new HashSet<int>(NbaDays());
        var start = new DateTime(2025, 10, 22);
        var games = GLeagueScheduleGenerator.BuildSchedule(Season(), ThirtyTeams(), allowed);

        foreach (var g in games)
        {
            Assert.That(allowed.Contains(g.game_day), Is.True, $"game_day {g.game_day} no tiene NBA");
            var date = start.AddDays(g.game_day - 1);
            bool allStarBreak = date.Month == 2 && date.Day >= 8 && date.Day <= 14;
            Assert.That(allStarBreak, Is.False, "La semana All-Star debe quedar vacía");
        }
    }

    [Test]
    public void BuildSchedule_TiposYCodificacionCorrectos()
    {
        var games = GLeagueScheduleGenerator.BuildSchedule(Season(), ThirtyTeams(), NbaDays());
        var sample = games.First();

        Assert.That(sample.game_type, Is.EqualTo(GLeagueScheduleGenerator.TYPE_REGULAR));
        Assert.That(sample.is_played, Is.EqualTo(0));
        Assert.That(sample.home_team_id, Is.GreaterThanOrEqualTo(Offset + 1));
        Assert.That(sample.home_team_id, Is.LessThanOrEqualTo(Offset + 30));
        Assert.That(sample.series_label, Is.EqualTo(""));
        Assert.That(sample.game_date, Does.Match(@"^\d{4}-\d{2}-\d{2}$"));
    }

    // ── PickSpreadDates ──────────────────────────────────

    [Test]
    public void PickSpreadDates_MenosFechasQueJornadas_Cicla()
    {
        var result = GLeagueScheduleGenerator.PickSpreadDates(new List<int> { 5, 10 }, 5);
        Assert.That(result.Count, Is.EqualTo(5));
        Assert.That(result, Has.Member(5).And.Member(10));
    }

    [Test]
    public void PickSpreadDates_OrdenPreservado()
    {
        var days = Enumerable.Range(0, 100).ToList();
        var result = GLeagueScheduleGenerator.PickSpreadDates(days, 10);
        Assert.That(result, Is.Ordered);
        Assert.That(result.Count, Is.EqualTo(10));
    }
}

public class GLeagueStandingsTests
{
    static List<GLeagueTeamData> FourEastTeams() => new List<GLeagueTeamData>
    {
        new GLeagueTeamData { id = 1, name = "A", conference = "East" },
        new GLeagueTeamData { id = 2, name = "B", conference = "East" },
        new GLeagueTeamData { id = 3, name = "C", conference = "East" },
        new GLeagueTeamData { id = 4, name = "D", conference = "East" },
    };

    static GameData GlGame(int home, int away, int hs, int as_, string type = "gleague", int played = 1)
        => new GameData
        {
            home_team_id = GLeagueHelper.EncodeGlTeamId(home),
            away_team_id = GLeagueHelper.EncodeGlTeamId(away),
            home_score = hs,
            away_score = as_,
            game_type = type,
            is_played = played,
            game_date = "2025-12-01",
            game_day = 50
        };

    [Test]
    public void Compute_DescodificaIdsYCuentaSoloJugados()
    {
        var teams = FourEastTeams();
        var games = new List<GameData>
        {
            GlGame(1, 2, 100, 90),
            GlGame(3, 1, 80, 110),
            GlGame(2, 4, 95, 95 + 20),   // gana 4
            GlGame(4, 3, 70, 60),        // jugado pero veremos
            GlGame(1, 4, 50, 50, played: 0), // no jugado: ignora
            GlGame(2, 3, 88, 80, type: "playoff"), // tipo NBA: ignora
        };

        var table = GLeagueStandings.Compute(teams, games);

        Assert.That(table[0].teamId, Is.EqualTo(1)); // 2-0
        Assert.That(table[0].wins, Is.EqualTo(2));
        Assert.That(table[1].teamId, Is.EqualTo(4)); // 2-0 también, mejor dif? A dif +40, D dif +25 y -10...
        Assert.That(table.Any(r => r.teamId == 3 && r.wins == 0), Is.True);

        // Nadie acumuló el partido no jugado ni el de otro tipo
        Assert.That(table.Sum(r => r.wins + r.losses), Is.EqualTo(8));
    }

    [Test]
    public void StreakText_UltimasCincoMasRecienteAlFinal()
    {
        var row = new GLeagueStandingRow();
        row.results.AddRange(new[] { true, true, false, true });
        Assert.That(GLeagueStandings.StreakText(row), Is.EqualTo("VVDV"));

        var longRow = new GLeagueStandingRow();
        longRow.results.AddRange(new[] { false, false, false, false, true, true, true, true, true, true });
        Assert.That(GLeagueStandings.StreakText(longRow, 5), Is.EqualTo("VVVVV"));
        Assert.That(GLeagueStandings.StreakText(new GLeagueStandingRow()), Is.EqualTo("—"));
    }
}

public class GLeaguePostSeasonTests
{
    static List<GLeagueTeamData> SmallLeague()
    {
        var teams = new List<GLeagueTeamData>();
        for (int i = 1; i <= 6; i++) teams.Add(new GLeagueTeamData { id = i, name = $"E{i}", conference = "East" });
        for (int i = 7; i <= 12; i++) teams.Add(new GLeagueTeamData { id = i, name = $"W{i}", conference = "West" });
        return teams;
    }

    static GameData Played(string type, int home, int away, int hs, int as_, string label = "")
        => new GameData
        {
            game_type = type,
            series_label = label,
            home_team_id = GLeagueHelper.EncodeGlTeamId(home),
            away_team_id = GLeagueHelper.EncodeGlTeamId(away),
            home_score = hs,
            away_score = as_,
            is_played = 1,
            game_day = 100,
            game_date = "2026-03-25"
        };

    [Test]
    public void ComputeRegularSeeds_OrdenPorConferencia()
    {
        var teams = SmallLeague();
        var games = new List<GameData>
        {
            Played(GLeagueScheduleGenerator.TYPE_REGULAR, 1, 2, 100, 80),
            Played(GLeagueScheduleGenerator.TYPE_REGULAR, 3, 4, 90, 95),
            Played(GLeagueScheduleGenerator.TYPE_REGULAR, 7, 8, 120, 60),
        };

        var seeds = GLeaguePostSeason.ComputeRegularSeeds(teams, games);

        // Solo los que tienen partidos reciben seed
        Assert.That(seeds.ContainsKey(1), Is.True);
        Assert.That(seeds.ContainsKey(7), Is.True);
        Assert.That(seeds.ContainsKey(9), Is.False);

        // El 7 tiene 100% y va antes que cualquier Este en SU conferencia,
        // las seeds son independientes por conferencia: ambos líderes son seed 1
        Assert.That(seeds[1].seed, Is.EqualTo(1));
        Assert.That(seeds[7].seed, Is.EqualTo(1));
        Assert.That(seeds[1].winPct, Is.EqualTo(1f).Within(0.001f));
    }

    [Test]
    public void WinnerOf_GanaLocalEmpateVaALocal()
    {
        var g = Played("gleague", 3, 4, 101, 99);
        Assert.That(GLeaguePostSeasonTestsAccess.WinnerForTest(g), Is.EqualTo(3));

        var tie = Played("gleague", 5, 6, 100, 100);
        Assert.That(GLeaguePostSeasonTestsAccess.WinnerForTest(tie), Is.EqualTo(5));
    }

    [Test]
    public void SeriesScoreForTeam_SumaSoloJugados()
    {
        // Serie mejor de 3 entre 2 y 3. G1 gana local(2), G2 gana visitante(3), G3 se genera 2-1: serie 2-1.
        var serie = new List<GameData>
        {
            Played("gleague_playoff", 2, 3, 110, 90, "gl-final"),
            Played("gleague_playoff", 2, 3, 90, 100, "gl-final"),
            Played("gleague_playoff", 2, 3, 105, 98, "gl-final"),
        };

        // Para el equipo 2 (local del G1) el parcial es 2-1.
        Assert.That(GLeaguePostSeason.SeriesScoreForTeam(serie, 2), Is.EqualTo("2-1"));
        Assert.That(GLeaguePostSeason.SeriesScoreForTeam(serie, 3), Is.EqualTo("1-2"));

        // Un partido sin jugar no suma.
        var unfinished = new List<GameData>
        {
            Played("gleague_playoff", 2, 3, 110, 90, "gl-final"),
            new GameData { game_type = "gleague_playoff", series_label = "gl-final",
                home_team_id = GLeagueHelper.EncodeGlTeamId(2), away_team_id = GLeagueHelper.EncodeGlTeamId(3),
                is_played = 0, home_score = 0, away_score = 0 }
        };
        Assert.That(GLeaguePostSeason.SeriesScoreForTeam(unfinished, 2), Is.EqualTo("1-0"));
    }

    [Test]
    public void SeriesResult_FormatoLocalVisitante()
    {
        var serie = new List<GameData>
        {
            Played("gleague_playoff", 4, 5, 90, 110, "gl-cf-east"),
            Played("gleague_playoff", 4, 5, 100, 95, "gl-cf-east"),
            Played("gleague_playoff", 4, 5, 80, 99, "gl-cf-east"),
        };
        // local(4) ganó G1 y G3; visitante(5) ganó G2 → 2-1 (local-visitante)
        Assert.That(GLeaguePostSeason.SeriesResult(serie), Is.EqualTo("2-1"));
        Assert.That(GLeaguePostSeason.SeriesResult(new List<GameData>()), Is.EqualTo("0-0"));
    }
}

/// <summary>Acceso a la semántica de WinnerOf (privada) para tests.</summary>
public static class GLeaguePostSeasonTestsAccess
{
    public static int WinnerForTest(GameData g)
    {
        return g.home_score >= g.away_score
            ? GLeagueHelper.DecodeGlTeamId(g.home_team_id)
            : GLeagueHelper.DecodeGlTeamId(g.away_team_id);
    }
}
