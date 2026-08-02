using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Steppe.Editor
{
    public static class SteppeStandaloneBuilder
    {
        private const string OutputEnvironmentVariable =
            "STEPPE_BUILD_OUTPUT";

        [MenuItem("Steppe/Собрать Windows-версию")]
        public static void BuildWindowsStandalone()
        {
            var outputPath = Environment.GetEnvironmentVariable(
                OutputEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                outputPath = Path.Combine(
                    Directory.GetParent(Application.dataPath).FullName,
                    "Builds",
                    "Steppe-Windows",
                    "Steppe.exe");
            }

            outputPath = Path.GetFullPath(outputPath);
            var outputDirectory = Path.GetDirectoryName(outputPath);
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                throw new InvalidOperationException(
                    $"Некорректный путь сборки: {outputPath}");
            }

            Directory.CreateDirectory(outputDirectory);
            var scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
            if (scenes.Length == 0)
            {
                throw new InvalidOperationException(
                    "В Build Settings нет включённых сцен.");
            }

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Сборка завершилась со статусом {report.summary.result}. "
                    + $"Ошибок: {report.summary.totalErrors}.");
            }

            Debug.Log(
                $"Steppe Windows build: {outputPath} "
                + $"({report.summary.totalSize / (1024f * 1024f):F1} MB)");
        }
    }
}
