using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// F0-02 · Línea base diagnóstica del calendario NBA.
/// Ejecuta el generador real contra un slot temporal y reporta sus invariantes; no
/// convierte desviaciones de localía/partidos en fallos ni modifica el generador.
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
    public void GenerateSchedule_ReportsReproducibleDiagnosticBaseline()
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
        sb.AppendLine("Nota: informe diagnóstico, no una aserción de balance. No se modificó ScheduleGenerator.");
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
