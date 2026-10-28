using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Random = UnityEngine.Random;

/// <summary>
/// F0-02/F5-01 · Invariantes del calendario NBA.
/// Ejecuta el generador real contra un slot temporal y exige 82 partidos por equipo,
/// 41 localías/41 visitas, 1.230 partidos y 2-2 en cada pareja divisional.
/// </summary>
public class ScheduleGeneratorDiagnosticTests
{
    const int DIAGNOSTIC_SEED = 20261023;
    const int SEASON_ID = 9026;
    const int MANAGER_ID = 1;

    string _root;
    GameObject _go;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "tf_schedulediag_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);

        GameSaveManager.BaseDirOverride  = Path.Combine(_root, "saves");
        DatabaseManager.DataRootOverride = Path.Combine(_root, "data");
        CustomScopes.RootOverride        = Path.Combine(_root, "custom");

        _go = new GameObject("DatabaseManagerScheduleDiagnostic");
        var db = _go.AddComponent<DatabaseManager>();
        SetInstance(db);
    }

    [TearDown]
    public void TearDown()
    {
        DatabaseManager.Instance?.CloseActiveSlot();
        SetInstance(null);
        if (_go != null) UnityEngine.Object.DestroyImmediate(_go);

        GameSaveManager.BaseDirOverride  = null;
        DatabaseManager.DataRootOverride = null;
        CustomScopes.RootOverride        = null;
        try { if (Directory.Exists(_root)) Directory.Delete(_root, true); } catch { }
    }

    static void SetInstance(DatabaseManager db) =>
        typeof(DatabaseManager)
            .GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
            ?.SetValue(null, db);

    [Test]
    public void GenerateSchedule_MeetsNbaBalanceInvariants()
    {
        var db = DatabaseManager.Instance;
        Assert.That(db.InitSaveSlot(1), Is.True, "No se pudo crear el slot temporal: " + db.LastInitError);

        var teams = db.GetAllTeams();
        Assert.That(teams, Has.Count.EqualTo(30), "El diagnóstico requiere las 30 franquicias.");

        var season = new SeasonData
        {
            id = SEASON_ID,
            manager_id = MANAGER_ID,
            year_start = 2026,
            year_end = 2027,
            phase = "regular"
        };

        // El generador usa UnityEngine.Random. Fijamos una semilla para que el informe
        // sea reproducible y restauramos el estado global para no afectar otros tests.
        Random.State previousState = Random.state;
        int generated;
        List<GameData> games;
        try
        {
            Random.InitState(DIAGNOSTIC_SEED);
            generated = ScheduleGenerator.GenerateSchedule(season, teams);
            games = db.Db.Table<GameData>()
                .Where(g => g.season_id == SEASON_ID && g.manager_id == MANAGER_ID && g.game_type == "regular")
                .ToList();
        }
        finally
        {
            Random.state = previousState;
        }

        Assert.That(generated, Is.GreaterThanOrEqualTo(0), "El generador no pudo guardar el calendario.");
        Assert.That(games, Is.Not.Empty, "El generador no guardó partidos regulares para la temporada sintética.");

        TestContext.WriteLine(BuildReport(teams, games, generated));

        // F5-01: invariantes exigentes (ya no es un informe diagnóstico).
        Assert.That(generated, Is.EqualTo(1230), "La NBA regular tiene 1.230 partidos.");
        Assert.That(games, Has.Count.EqualTo(1230), "El calendario guardado debe tener 1.230 partidos.");

        var gameCount = teams.ToDictionary(t => t.id, _ => 0);
        var homeCount = teams.ToDictionary(t => t.id, _ => 0);
        var awayCount = teams.ToDictionary(t => t.id, _ => 0);
        foreach (var game in games)
        {
            gameCount[game.home_team_id]++;
            gameCount[game.away_team_id]++;
            homeCount[game.home_team_id]++;
            awayCount[game.away_team_id]++;
        }

        foreach (var team in teams)
        {
            Assert.That(gameCount[team.id], Is.EqualTo(82), $"{team.abbreviation}: 82 partidos");
            Assert.That(homeCount[team.id], Is.EqualTo(41), $"{team.abbreviation}: 41 en casa");
            Assert.That(awayCount[team.id], Is.EqualTo(41), $"{team.abbreviation}: 41 fuera");
        }

        // Cada pareja divisional se reparte 2-2.
        foreach (var division in teams.GroupBy(t => new { t.conference, t.division }))
        {
            var clubs = division.OrderBy(t => t.id).ToList();
            for (int i = 0; i < clubs.Count; i++)
            for (int j = i + 1; j < clubs.Count; j++)
            {
                int aHome = games.Count(g => g.home_team_id == clubs[i].id && g.away_team_id == clubs[j].id);
                int bHome = games.Count(g => g.home_team_id == clubs[j].id && g.away_team_id == clubs[i].id);
                Assert.That(aHome, Is.EqualTo(2), $"{clubs[i].abbreviation}-{clubs[j].abbreviation}: 2 en casa para el primero");
                Assert.That(bHome, Is.EqualTo(2), $"{clubs[i].abbreviation}-{clubs[j].abbreviation}: 2 en casa para el segundo");
            }
        }

        // El reparto de cruces (2, 3 o 4) también queda cerrado.
        var expectedPairs = BuildExpectedPairs(teams);
        var actualPairs = new Dictionary<(int first, int second), int>();
        foreach (var game in games)
        {
            var key = PairKey(game.home_team_id, game.away_team_id);
            actualPairs[key] = actualPairs.TryGetValue(key, out int count) ? count + 1 : 1;
        }
        foreach (var expected in expectedPairs)
            Assert.That(actualPairs.GetValueOrDefault(expected.Key, 0), Is.EqualTo(expected.Value),
                $"Cruce {expected.Key.first}-{expected.Key.second}: partidos esperados");

        // F5-04: 60 partidos de grupo de la Cup, 4 por equipo y 2 en casa.
        var cupGames = games.Where(g => (g.series_label ?? "").StartsWith(CupHelper.GroupLabelPrefix)).ToList();
        Assert.That(cupGames, Has.Count.EqualTo(60), "NBA Cup: 60 partidos de grupo");
        TestContext.WriteLine($"NBA Cup: {cupGames.Count} partidos de grupo; último día {cupGames.Max(g => g.game_day)}.");
        var cupDays = CupHelper.GroupPlayDays(season);
        Assert.That(cupGames.All(g => cupDays.Contains(g.game_day)), Is.True,
            "NBA Cup: todos los partidos de grupo deben caer en las noches de Cup.");
        Assert.That(cupGames.Max(g => g.game_day), Is.LessThanOrEqualTo(cupDays.Max()),
            "NBA Cup: la fase de grupos debe terminar antes de diciembre.");
        foreach (var team in teams)
        {
            var ownCup = cupGames.Where(g => g.home_team_id == team.id || g.away_team_id == team.id).ToList();
            Assert.That(ownCup, Has.Count.EqualTo(4), $"{team.abbreviation}: 4 partidos de Cup");
            Assert.That(ownCup.Count(g => g.home_team_id == team.id), Is.EqualTo(2),
                $"{team.abbreviation}: 2 locales de Cup");
            Assert.That(ownCup.Count(g => g.away_team_id == team.id), Is.EqualTo(2),
                $"{team.abbreviation}: 2 visitas de Cup");
        }

        // F5-03: ningún back-to-back cruza el país sin día de descanso.
        var teamsById = teams.ToDictionary(t => t.id);
        foreach (var team in teams)
        {
            var teamGames = games
                .Where(g => g.home_team_id == team.id || g.away_team_id == team.id)
                .OrderBy(g => g.game_day)
                .ToList();
            for (int i = 1; i < teamGames.Count; i++)
            {
                var previous = teamGames[i - 1];
                var current = teamGames[i];
                int kilometers = TravelHelper.DistanceKilometers(previous.home_team_id, current.home_team_id, teamsById);
                Assert.That(TravelHelper.IsImpossibleBackToBack(previous.game_day, current.game_day, kilometers),
                    Is.False,
                    $"{team.abbreviation}: back-to-back {previous.game_day}->{current.game_day} de {kilometers:N0} km");
            }

            Assert.That(MaxConsecutiveGames(teamGames), Is.LessThanOrEqualTo(2),
                $"{team.abbreviation}: más de 2 días consecutivos");
            Assert.That(HasMoreThanFourInCalendarWeek(teamGames, season), Is.False,
                $"{team.abbreviation}: más de 4 partidos en una semana natural");
        }
    }

    [Test]
    public void GenerateSchedule_ThirtySeeds_KeepBalanceAndTravelLimits()
    {
        var db = DatabaseManager.Instance;
        Assert.That(db.InitSaveSlot(1), Is.True, "No se pudo crear el slot temporal: " + db.LastInitError);

        var teams = db.GetAllTeams();
        var teamsById = teams.ToDictionary(t => t.id);
        Random.State previousState = Random.state;
        try
        {
            for (int i = 0; i < 30; i++)
            {
                var season = new SeasonData
                {
                    id = SEASON_ID + 100 + i,
                    manager_id = MANAGER_ID,
                    year_start = 2026,
                    year_end = 2027,
                    phase = "regular"
                };

                Random.InitState(DIAGNOSTIC_SEED + i);
                int generated = ScheduleGenerator.GenerateSchedule(season, teams);
                Assert.That(generated, Is.EqualTo(1230), $"semilla {i}: 1.230 partidos");

                var games = db.Db.Table<GameData>()
                    .Where(g => g.season_id == season.id && g.game_type == "regular")
                    .ToList();
                Assert.That(games, Has.Count.EqualTo(1230), $"semilla {i}: partidos guardados");

                foreach (var team in teams)
                {
                    var teamGames = games
                        .Where(g => g.home_team_id == team.id || g.away_team_id == team.id)
                        .OrderBy(g => g.game_day)
                        .ToList();
                    Assert.That(teamGames, Has.Count.EqualTo(82), $"semilla {i}: {team.abbreviation} PJ");
                    Assert.That(teamGames.Count(g => g.home_team_id == team.id), Is.EqualTo(41),
                        $"semilla {i}: {team.abbreviation} casa");
                    Assert.That(teamGames.Count(g => g.away_team_id == team.id), Is.EqualTo(41),
                        $"semilla {i}: {team.abbreviation} fuera");

                    for (int j = 1; j < teamGames.Count; j++)
                    {
                        var previous = teamGames[j - 1];
                        var current = teamGames[j];
                        int kilometers = TravelHelper.DistanceKilometers(previous.home_team_id, current.home_team_id, teamsById);
                        Assert.That(TravelHelper.IsImpossibleBackToBack(previous.game_day, current.game_day, kilometers),
                            Is.False, $"semilla {i}: {team.abbreviation} B2B {previous.game_day}->{current.game_day} ({kilometers:N0} km)");
                    }

                    Assert.That(MaxConsecutiveGames(teamGames), Is.LessThanOrEqualTo(2),
                        $"semilla {i}: {team.abbreviation} más de 2 días consecutivos");
                    Assert.That(HasMoreThanFourInCalendarWeek(teamGames, season), Is.False,
                        $"semilla {i}: {team.abbreviation} más de 4 partidos en una semana natural");
                }
            }
        }
        finally
        {
            Random.state = previousState;
        }
    }

    [Test]
    public void GenerateSchedule_IncompleteLeague_FailsBeforeSaving()
    {
        var db = DatabaseManager.Instance;
        Assert.That(db.InitSaveSlot(1), Is.True, "No se pudo crear el slot temporal: " + db.LastInitError);

        var teams = db.GetAllTeams();
        teams.RemoveAt(0); // liga incompleta: no puede cumplir 1.230/82
        var season = new SeasonData
        {
            id = SEASON_ID + 1,
            manager_id = MANAGER_ID,
            year_start = 2026,
            year_end = 2027,
            phase = "regular"
        };

        LogAssert.Expect(LogType.Error, new Regex(@"\[Schedule\] Se programaron \d+ partidos; se esperaban \d+\."));

        Random.State previousState = Random.state;
        int generated;
        try
        {
            Random.InitState(DIAGNOSTIC_SEED);
            generated = ScheduleGenerator.GenerateSchedule(season, teams);
        }
        finally
        {
            Random.state = previousState;
        }

        Assert.That(generated, Is.EqualTo(-1), "Un calendario incompleto debe fallar, no guardarse.");
        Assert.That(db.Db.Table<GameData>().Count(g => g.season_id == season.id), Is.Zero,
            "No debe quedar ningún partido guardado de una generación fallida.");
    }

    [Test]
    public void Cup_AdvancesFromGroupsToChampion()
    {
        var db = DatabaseManager.Instance;
        Assert.That(db.InitSaveSlot(1), Is.True, "No se pudo crear el slot temporal: " + db.LastInitError);

        var teams = db.GetAllTeams();
        var season = new SeasonData
        {
            id = SEASON_ID + 300,
            manager_id = MANAGER_ID,
            year_start = 2026,
            year_end = 2027,
            phase = "regular",
            current_game_day = 60,
            current_date = "2026-12-20"
        };

        Random.State previousState = Random.state;
        int generated;
        try
        {
            Random.InitState(DIAGNOSTIC_SEED);
            generated = ScheduleGenerator.GenerateSchedule(season, teams);
        }
        finally
        {
            Random.state = previousState;
        }
        Assert.That(generated, Is.EqualTo(1230));

        // Sin grupos jugados no se crea eliminatoria.
        Assert.That(CupHelper.AdvanceIfNeeded(season, teams), Is.Zero);
        var edition = db.GetCupEdition(season.id);
        Assert.That(edition, Is.Not.Null);
        Assert.That(edition.status, Is.EqualTo("group"));

        // Resolver los 60 partidos de grupo con marcadores sintéticos.
        var groupGames = db.GetCupGames(season.id, MANAGER_ID)
            .Where(g => (g.series_label ?? "").StartsWith(CupHelper.GroupLabelPrefix)).ToList();
        Assert.That(groupGames, Has.Count.EqualTo(60));
        foreach (var game in groupGames)
        {
            game.home_score = 100;
            game.away_score = 90;
            game.is_played = 1;
            db.UpdateGame(game);
        }

        Assert.That(CupHelper.AdvanceIfNeeded(season, teams), Is.EqualTo(4), "cuartos de final");
        Assert.That(db.GetCupEdition(season.id).status, Is.EqualTo("knockout"));

        PlayCupRound(db, season, CupHelper.QfPrefix, homeWins: true);
        Assert.That(CupHelper.AdvanceIfNeeded(season, teams), Is.EqualTo(2), "semifinales");

        PlayCupRound(db, season, CupHelper.SfPrefix, homeWins: true);
        Assert.That(CupHelper.AdvanceIfNeeded(season, teams), Is.EqualTo(1), "final");

        PlayCupRound(db, season, CupHelper.FinalLabel, homeWins: true);
        CupHelper.AdvanceIfNeeded(season, teams);

        var finished = db.GetCupEdition(season.id);
        Assert.That(finished.status, Is.EqualTo("finished"));
        Assert.That(finished.champion_team_id, Is.GreaterThan(0));
    }

    static void PlayCupRound(DatabaseManager db, SeasonData season, string labelPrefix, bool homeWins)
    {
        var games = db.GetCupGames(season.id, season.manager_id)
            .Where(g => (g.series_label ?? "").StartsWith(labelPrefix) && g.is_played == 0)
            .ToList();
        Assert.That(games, Is.Not.Empty, $"ronda {labelPrefix} sin partidos");
        foreach (var game in games)
        {
            game.home_score = homeWins ? 110 : 90;
            game.away_score = homeWins ? 90 : 110;
            game.is_played = 1;
            db.UpdateGame(game);
        }
    }

    static int MaxConsecutiveGames(List<GameData> teamGames)
    {
        int maximum = 1;
        int current = 1;
        for (int i = 1; i < teamGames.Count; i++)
        {
            current = teamGames[i].game_day == teamGames[i - 1].game_day + 1 ? current + 1 : 1;
            if (current > maximum) maximum = current;
        }
        return maximum;
    }

    static bool HasMoreThanFourInCalendarWeek(List<GameData> teamGames, SeasonData season)
    {
        var start = new DateTime(season.year_start, 10, 22);
        int offset = ((int)start.DayOfWeek + 6) % 7;
        var counts = new Dictionary<int, int>();
        foreach (var game in teamGames)
        {
            int week = (game.game_day - 1 + offset) / 7;
            counts[week] = counts.TryGetValue(week, out int count) ? count + 1 : 1;
        }
        return counts.Values.Any(count => count > 4);
    }

    static string BuildReport(List<TeamData> teams, List<GameData> games, int generated)
    {
        var teamById = teams.ToDictionary(t => t.id);
        var gameCount = teams.ToDictionary(t => t.id, _ => 0);
        var homeCount = teams.ToDictionary(t => t.id, _ => 0);
        var awayCount = teams.ToDictionary(t => t.id, _ => 0);
        var actualPairs = new Dictionary<(int first, int second), int>();

        foreach (var game in games)
        {
            if (!gameCount.ContainsKey(game.home_team_id) || !gameCount.ContainsKey(game.away_team_id))
                continue;

            gameCount[game.home_team_id]++;
            gameCount[game.away_team_id]++;
            homeCount[game.home_team_id]++;
            awayCount[game.away_team_id]++;

            var key = PairKey(game.home_team_id, game.away_team_id);
            actualPairs[key] = actualPairs.TryGetValue(key, out int count) ? count + 1 : 1;
        }

        var expectedPairs = BuildExpectedPairs(teams);
        var shortPairs = expectedPairs
            .Where(pair => actualPairs.GetValueOrDefault(pair.Key, 0) < pair.Value)
            .OrderBy(pair => pair.Key.first)
            .ThenBy(pair => pair.Key.second)
            .ToList();
        var excessPairs = expectedPairs
            .Where(pair => actualPairs.GetValueOrDefault(pair.Key, 0) > pair.Value)
            .OrderBy(pair => pair.Key.first)
            .ThenBy(pair => pair.Key.second)
            .ToList();

        int expectedTotal = expectedPairs.Values.Sum();
        int missingGames = shortPairs.Sum(pair => pair.Value - actualPairs.GetValueOrDefault(pair.Key, 0));
        int excessGames = excessPairs.Sum(pair => actualPairs.GetValueOrDefault(pair.Key, 0) - pair.Value);

        var sb = new StringBuilder();
        sb.AppendLine($"DIAGNÓSTICO DE CALENDARIO · semilla {DIAGNOSTIC_SEED} · temporada sintética 2026-27");
        sb.AppendLine($"Partidos regulares: generador={generated} · guardados={games.Count} · esperados={expectedTotal} (NBA: 1.230).");
        sb.AppendLine("Equipo | PJ | Casa | Fuera | Diferencia vs 82");
        foreach (var team in teams.OrderBy(t => t.abbreviation))
        {
            int played = gameCount[team.id];
            int difference = played - 82;
            string differenceText = difference > 0 ? $"+{difference}" : difference.ToString();
            sb.AppendLine($"{team.abbreviation,-4} | {played,2} | {homeCount[team.id],4} | {awayCount[team.id],5} | {differenceText}");
        }

        sb.AppendLine($"Parejas: {expectedPairs.Count} esperadas · {shortPairs.Count} incompletas ({missingGames} partidos faltantes) · {excessPairs.Count} con partidos de más ({excessGames}).");
        AppendPairExamples(sb, "Cruces incompletos", shortPairs, actualPairs, teamById);
        AppendPairExamples(sb, "Cruces con exceso", excessPairs, actualPairs, teamById);
        sb.AppendLine("Nota: el informe se publica como contexto; las invariantes de F5-01 se exigen con asserts.");
        return sb.ToString();
    }

    static Dictionary<(int first, int second), int> BuildExpectedPairs(List<TeamData> teams)
    {
        var expected = new Dictionary<(int first, int second), int>();
        void Add(TeamData a, TeamData b, int count) => expected[PairKey(a.id, b.id)] = count;

        foreach (var conference in teams.GroupBy(t => t.conference))
        {
            var divisions = conference
                .GroupBy(t => t.division)
                .ToDictionary(g => g.Key, g => g.OrderBy(t => t.id).ToList());
            var divisionNames = divisions.Keys.OrderBy(name => name).ToList();

            // La liga regular incluye cuatro partidos entre rivales de la misma división.
            foreach (var division in divisions.Values)
            for (int i = 0; i < division.Count; i++)
            for (int j = i + 1; j < division.Count; j++)
                Add(division[i], division[j], 4);

            // Entre divisiones de la misma conferencia, el generador asigna tres o cuatro.
            for (int d1 = 0; d1 < divisionNames.Count; d1++)
            for (int d2 = d1 + 1; d2 < divisionNames.Count; d2++)
            {
                var a = divisions[divisionNames[d1]];
                var b = divisions[divisionNames[d2]];
                int n = a.Count;
                for (int i = 0; i < a.Count; i++)
                for (int j = 0; j < b.Count; j++)
                {
                    int offset = ((j - i) % n + n) % n;
                    Add(a[i], b[j], offset < 3 ? 4 : 3);
                }
            }
        }

        var east = teams.Where(t => t.conference == "East").ToList();
        var west = teams.Where(t => t.conference == "West").ToList();
        foreach (var a in east)
        foreach (var b in west)
            Add(a, b, 2);

        return expected;
    }

    static void AppendPairExamples(StringBuilder sb, string heading,
        List<KeyValuePair<(int first, int second), int>> pairs,
        Dictionary<(int first, int second), int> actual,
        Dictionary<int, TeamData> teams)
    {
        sb.AppendLine($"{heading}: {pairs.Count}.");
        foreach (var pair in pairs.Take(12))
        {
            int count = actual.GetValueOrDefault(pair.Key, 0);
            sb.AppendLine($"  {teams[pair.Key.first].abbreviation}-{teams[pair.Key.second].abbreviation}: {count}/{pair.Value} partidos.");
        }
        if (pairs.Count > 12)
            sb.AppendLine($"  … y {pairs.Count - 12} más.");
    }

    static (int first, int second) PairKey(int a, int b) => a < b ? (a, b) : (b, a);
}
