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
        public static void BuildAndroid()
        {
            ProjectSetup.Apply();
            // -bonkadooArm64: smaller local test APK; release keeps ARMv7 for low-tier devices
            if (Arg("-bonkadooArm64")) PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            Build(BuildTarget.Android, "Builds/Android/Bonkadoo.apk", Arg("-bonkadooDev"));
        }

        /// <summary>Xcode project; <c>-bonkadooSimulator</c> targets the iOS Simulator SDK (no signing needed).</summary>
        public static void BuildIOS()
        {
            PlayerSettings.iOS.sdkVersion = Arg("-bonkadooSimulator") ? iOSSdkVersion.SimulatorSDK : iOSSdkVersion.DeviceSDK;
            Build(BuildTarget.iOS, Arg("-bonkadooSimulator") ? "Builds/iOS-Simulator" : "Builds/iOS", Arg("-bonkadooDev"));
        }

        private static void Build(BuildTarget target, string output, bool development)
        {
            if (target != BuildTarget.Android) ProjectSetup.Apply();
            WriteAdUnitConfig();
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

        /// <summary>Real ad unit IDs only exist in CI secrets; the file is gitignored and absent locally.</summary>
        private static void WriteAdUnitConfig()
        {
            const string path = "Assets/Bonkadoo/Content/Resources/admob_ids.json";
            var config = new Bonkadoo.Infrastructure.Unity.AdMobConfig();
            config.androidBanner = ProjectSetup.Env("BONKADOO_ADMOB_ANDROID_BANNER", config.androidBanner);
            config.androidInterstitial = ProjectSetup.Env("BONKADOO_ADMOB_ANDROID_INTERSTITIAL", config.androidInterstitial);
            config.iosBanner = ProjectSetup.Env("BONKADOO_ADMOB_IOS_BANNER", config.iosBanner);
            config.iosInterstitial = ProjectSetup.Env("BONKADOO_ADMOB_IOS_INTERSTITIAL", config.iosInterstitial);
            File.WriteAllText(path, JsonUtility.ToJson(config));
            AssetDatabase.ImportAsset(path);
        }

        private static bool Arg(string name) => Environment.GetCommandLineArgs().Contains(name);
    }
}
