using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Interfaz de Editor para ejecutar el banco aislado de la partida guardada.</summary>
public sealed class MonteCarloLabWindow : EditorWindow
{
    int _seasons = 1;
    int _teams = 30;
    int _gamesPerTeam = 82;
    string _seed = "20261024";
    string _report;
    string _reportPath;
    Vector2 _scroll;

    [MenuItem("Tactical Five/Laboratorio Monte Carlo")]
    static void Open()
    {
        var window = GetWindow<MonteCarloLabWindow>("Monte Carlo");
        window.minSize = new Vector2(520, 460);
        window.Show();
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Laboratorio Monte Carlo", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Simula temporadas sintéticas en memoria con semilla fija. No abre ni modifica la partida guardada.",
            MessageType.Info);

        _seasons = EditorGUILayout.IntField("Temporadas", _seasons);
        _teams = EditorGUILayout.IntField("Equipos (número par)", _teams);
        _gamesPerTeam = EditorGUILayout.IntField("Partidos por equipo", _gamesPerTeam);
        _seed = EditorGUILayout.TextField("Semilla (uint64)", _seed);

        using (new EditorGUI.DisabledScope(!ulong.TryParse(_seed, out _)))
        {
            if (GUILayout.Button("SIMULAR Y GENERAR INFORME", GUILayout.Height(32)))
                Run();
        }

        if (!string.IsNullOrEmpty(_report))
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Informe generado: {_reportPath}", EditorStyles.miniLabel);
            using (var scroll = new EditorGUILayout.ScrollViewScope(_scroll, GUILayout.ExpandHeight(true)))
            {
                _scroll = scroll.scrollPosition;
                EditorGUILayout.TextArea(_report, GUILayout.ExpandHeight(true));
            }
            if (GUILayout.Button("REVELAR INFORME EN EL EXPLORADOR"))
                EditorUtility.RevealInFinder(_reportPath);
        }
    }

    void Run()
    {
        if (!ulong.TryParse(_seed, out ulong parsedSeed)) return;
        try
        {
            var report = MonteCarloRunner.Run(new MonteCarloConfig
            {
                seasons = _seasons,
                teams = _teams,
                gamesPerTeam = _gamesPerTeam,
                seed = parsedSeed
            });
            _report = report.ToMarkdown();
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            _reportPath = Path.Combine(projectRoot, "Library", "TacticalFive", "MonteCarloReport.md");
            Directory.CreateDirectory(Path.GetDirectoryName(_reportPath));
            File.WriteAllText(_reportPath, _report);
            Debug.Log($"[Monte Carlo] Informe generado: {_reportPath}\n{_report}");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("Monte Carlo", ex.Message, "Cerrar");
        }
        Repaint();
    }
}
