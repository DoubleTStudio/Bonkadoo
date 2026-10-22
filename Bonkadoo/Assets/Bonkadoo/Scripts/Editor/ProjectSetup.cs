using System;
using System.IO;
using System.Linq;
using Bonkadoo.Presentation;
using Bonkadoo.Presentation.Screens;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Bonkadoo.Editor
{
    /// <summary>Idempotent project configuration; run from the menu or -executeMethod in CI.</summary>
    public static class ProjectSetup
    {
        public const string ScenesDir = "Assets/Bonkadoo/Scenes";

        public static readonly (string name, Type root)[] SceneRoots =
        {
            (Nav.Bootstrap, typeof(GameBootstrap)), (Nav.Menu, typeof(MenuScreen)),
            (Nav.ChapterHub, typeof(ChapterHubScreen)), (Nav.Gameplay, typeof(GameplayScreen)),
            (Nav.Credits, typeof(CreditsScreen)),
        };

        [MenuItem("Bonkadoo/Apply Project Setup")]
        public static void Apply()
        {
            ConfigurePlayer();
            ConfigureAdMob();
            EnsureScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("[Bonkadoo] project setup applied");
        }

        private static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Omnicasa";
            PlayerSettings.productName = "BONKADOO!";
            PlayerSettings.bundleVersion = "0.9.0";
            PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.iOS.buildNumber = "1";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.omnicasa.bonkadoo");
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS, "com.omnicasa.bonkadoo");
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            // offline-first: no internet permission is required to play (D06); IAP/ads add their own when integrated
            PlayerSettings.Android.forceInternetPermission = false;
        }

        /// <summary>
        /// App IDs come from CI secrets (BONKADOO_ADMOB_ANDROID_APP_ID / _IOS_APP_ID); Google's sample app IDs keep
        /// local builds working. GMA's settings class is internal, so it is reached by reflection.
        /// </summary>
        private static void ConfigureAdMob()
        {
            var type = Type.GetType("GoogleMobileAds.Editor.GoogleMobileAdsSettings, GoogleMobileAds.Editor");
            var load = type?.GetMethod("LoadInstance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic |
                                                       System.Reflection.BindingFlags.Public);
            if (load == null) return;
            var settings = load.Invoke(null, null) as ScriptableObject;
            if (settings == null) return;
            var so = new SerializedObject(settings);
            Set(so, "adMobAndroidAppId", Env("BONKADOO_ADMOB_ANDROID_APP_ID", Bonkadoo.Infrastructure.Unity.AdMobConfig.SampleAndroidAppId));
            Set(so, "adMobIOSAppId", Env("BONKADOO_ADMOB_IOS_APP_ID", Bonkadoo.Infrastructure.Unity.AdMobConfig.SampleIosAppId));
            Set(so, "userTrackingUsageDescription",
                "Your data is used to show ads that are more relevant. The game works the same if you decline.");
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
        }

        private static void Set(SerializedObject so, string field, string value)
        {
            var prop = so.FindProperty(field);
            if (prop != null) prop.stringValue = value;
        }

        internal static string Env(string name, string fallback)
        {
            var v = Environment.GetEnvironmentVariable(name);
            return string.IsNullOrWhiteSpace(v) ? fallback : v.Trim();
        }

        private static void EnsureScenes()
        {
            Directory.CreateDirectory(ScenesDir);
            foreach (var (name, root) in SceneRoots)
            {
                var path = $"{ScenesDir}/{name}.unity";
                var scene = File.Exists(path)
                    ? EditorSceneManager.OpenScene(path, OpenSceneMode.Single)
                    : EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                if (UnityEngine.Object.FindAnyObjectByType(root) == null) new GameObject(root.Name).AddComponent(root);
                var cam = Camera.main;
                if (cam != null)
                {
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = new Color(0.13f, 0.13f, 0.23f);
                }
                EditorSceneManager.SaveScene(scene, path);
            }
            EditorBuildSettings.scenes = SceneRoots.Select(s => new EditorBuildSettingsScene($"{ScenesDir}/{s.name}.unity", true)).ToArray();
            if (File.Exists("Assets/Scenes/SampleScene.unity")) AssetDatabase.DeleteAsset("Assets/Scenes");
        }
    }
}
