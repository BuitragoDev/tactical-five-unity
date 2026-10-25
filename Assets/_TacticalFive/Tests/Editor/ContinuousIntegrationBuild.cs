using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

/// <summary>Entry point headless del pipeline CI (F3-07).</summary>
public static class ContinuousIntegrationBuild
{
    public static void BuildLinuxPlayer()
    {
        string outputPath = Environment.GetEnvironmentVariable("TACTICALFIVE_BUILD_PATH");
        if (string.IsNullOrWhiteSpace(outputPath))
            throw new InvalidOperationException("Define TACTICALFIVE_BUILD_PATH para la salida del build.");

        var scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();
        if (scenes.Length == 0)
            throw new InvalidOperationException("No hay escenas activas en EditorBuildSettings.");

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outputPath,
            target = BuildTarget.StandaloneLinux64,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
            throw new BuildFailedException(
                $"Build Linux fallido: {report.summary.result}; errores={report.summary.totalErrors}; "
                + $"advertencias={report.summary.totalWarnings}; salida={outputPath}");

        UnityEngine.Debug.Log($"[CI] Build Linux correcto: {outputPath} ({report.summary.totalSize} bytes).");
    }
}
