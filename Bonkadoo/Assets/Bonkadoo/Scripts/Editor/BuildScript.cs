using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Bonkadoo.Editor
{
    /// <summary>CI entry points. Signing material is never in the repo; without it Android uses Unity's debug keystore.</summary>
    public static class BuildScript
    {
        public static void BuildAndroid() => Build(BuildTarget.Android, "Builds/Android/Bonkadoo.apk", Arg("-bonkadooDev"));

        public static void BuildIOS() => Build(BuildTarget.iOS, "Builds/iOS", Arg("-bonkadooDev"));

        private static void Build(BuildTarget target, string output, bool development)
        {
            ProjectSetup.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(output) ?? ".");
            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = output,
                target = target,
                options = development ? BuildOptions.Development : BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log($"[Bonkadoo] build {target}: {summary.result}, {summary.totalSize / (1024 * 1024)} MB, " +
                      $"{summary.totalErrors} errors, {summary.totalTime}");
            if (summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }

        private static bool Arg(string name) => Environment.GetCommandLineArgs().Contains(name);
    }
}
